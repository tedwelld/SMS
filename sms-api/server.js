const cors = require('cors');
const express = require('express');
const fs = require('fs');
const path = require('path');

const app = express();
const PORT = process.env.PORT || 3000;
const DB_PATH = path.join(__dirname, 'db.json');

const POINT_VALUE = 0.05;

app.use(cors({ origin: ['http://localhost:4200', 'http://127.0.0.1:4200'] }));
app.use(express.json());

function readDb() {
  const raw = fs.readFileSync(DB_PATH, 'utf-8');
  return JSON.parse(raw);
}

function writeDb(db) {
  fs.writeFileSync(DB_PATH, `${JSON.stringify(db, null, 2)}\n`, 'utf-8');
}

function uid(prefix) {
  return `${prefix}-${Math.random().toString(36).slice(2, 8)}-${Date.now().toString(36)}`;
}

function roundMoney(value) {
  return Math.round(value * 100) / 100;
}

function normalizePhone(phone) {
  return String(phone || '').replace(/\D/g, '');
}

function lineTotals(quantity, product) {
  const baseSubtotal = product.price * quantity;
  let discount = 0;

  if (product.promo.type === 'discount') {
    discount = baseSubtotal * (Math.max(0, Math.min(product.promo.value, 90)) / 100);
  }

  if (product.promo.type === 'bogo') {
    discount = Math.floor(quantity / 2) * product.price;
  }

  const taxableSubtotal = Math.max(0, baseSubtotal - discount);
  const tax = taxableSubtotal * product.taxRate;

  return { discount, taxableSubtotal, tax };
}

function addAudit(db, action, userRole) {
  db.auditLogs.unshift({
    id: uid('log'),
    timestamp: new Date().toISOString(),
    userRole: userRole || db.settings.activeRole,
    action
  });
}

function draftPurchaseOrders(db) {
  const ordersByVendor = new Map();

  for (const product of db.products) {
    if (!(product.staple && product.stock <= product.minStock)) {
      continue;
    }

    const vendor = db.vendors.find((item) => item.departments.includes(product.department));
    if (!vendor) {
      continue;
    }

    const line = {
      productId: product.id,
      name: product.name,
      sku: product.sku,
      currentStock: product.stock,
      suggestedOrderQty: Math.max(product.minStock * 2 - product.stock, product.minStock)
    };

    if (!ordersByVendor.has(vendor.id)) {
      ordersByVendor.set(vendor.id, {
        id: uid('po'),
        vendorId: vendor.id,
        vendorName: vendor.name,
        createdAt: new Date().toISOString(),
        lines: [line]
      });
    } else {
      ordersByVendor.get(vendor.id).lines.push(line);
    }
  }

  return Array.from(ordersByVendor.values());
}

function eodReport(db) {
  const report = db.payments.reduce(
    (acc, payment) => {
      if (payment.method === 'Cash') {
        acc.cash += payment.amount;
      }
      if (payment.method === 'Card') {
        acc.card += payment.amount;
      }
      if (payment.method === 'Digital') {
        acc.digital += payment.amount;
      }

      acc.total += payment.amount;
      return acc;
    },
    { cash: 0, card: 0, digital: 0, total: 0 }
  );

  return {
    cash: roundMoney(report.cash),
    card: roundMoney(report.card),
    digital: roundMoney(report.digital),
    total: roundMoney(report.total),
    transactions: db.payments.length
  };
}

function shrinkageReport(db) {
  return db.products.map((product) => ({
    product: product.name,
    sku: product.sku,
    systemStock: product.stock,
    physicalCount: product.physicalCount,
    variance: product.physicalCount - product.stock
  }));
}

function bootstrapPayload(db) {
  return {
    settings: db.settings,
    products: db.products,
    customers: db.customers,
    vendors: db.vendors,
    draftPurchaseOrders: draftPurchaseOrders(db),
    salesTrend: db.salesTrend,
    reports: {
      eod: eodReport(db),
      shrinkage: shrinkageReport(db)
    },
    auditLogs: db.auditLogs.slice(0, 20)
  };
}

app.get('/health', (_req, res) => {
  res.json({ ok: true, service: 'sms-api' });
});

app.get('/api/bootstrap', (_req, res) => {
  const db = readDb();
  res.json(bootstrapPayload(db));
});

app.patch('/api/settings', (req, res) => {
  const db = readDb();
  const { activeRole, offlineMode } = req.body || {};

  if (typeof activeRole === 'string' && db.settings.roles.includes(activeRole)) {
    db.settings.activeRole = activeRole;
  }

  if (typeof offlineMode === 'boolean') {
    db.settings.offlineMode = offlineMode;
    addAudit(
      db,
      `Offline mode ${offlineMode ? 'enabled' : 'disabled'}.`,
      db.settings.activeRole
    );
  }

  writeDb(db);
  res.json(db.settings);
});

app.get('/api/products', (req, res) => {
  const db = readDb();
  const term = String(req.query.search || '').trim().toLowerCase();

  if (!term) {
    return res.json(db.products);
  }

  const filtered = db.products.filter(
    (item) => item.name.toLowerCase().includes(term) || item.sku.toLowerCase().includes(term)
  );

  return res.json(filtered);
});

app.patch('/api/products/:productId/physical-count', (req, res) => {
  const db = readDb();
  const { productId } = req.params;
  const nextCount = Number(req.body?.physicalCount);

  if (!Number.isFinite(nextCount) || nextCount < 0) {
    return res.status(400).json({ message: 'Invalid physical count.' });
  }

  const product = db.products.find((item) => item.id === productId);
  if (!product) {
    return res.status(404).json({ message: 'Product not found.' });
  }

  product.physicalCount = Math.floor(nextCount);
  addAudit(db, `Updated physical count for ${product.sku}.`, db.settings.activeRole);
  writeDb(db);

  return res.json({ product, draftPurchaseOrders: draftPurchaseOrders(db) });
});

app.patch('/api/products/:productId/promotion', (req, res) => {
  const db = readDb();
  const { productId } = req.params;
  const promoType = req.body?.type;
  const rawValue = Number(req.body?.value ?? 0);

  if (!['none', 'discount', 'bogo'].includes(promoType)) {
    return res.status(400).json({ message: 'Invalid promotion type.' });
  }

  const product = db.products.find((item) => item.id === productId);
  if (!product) {
    return res.status(404).json({ message: 'Product not found.' });
  }

  product.promo = {
    type: promoType,
    value: promoType === 'discount' ? Math.max(0, Math.min(rawValue, 90)) : 0
  };

  addAudit(db, `Updated promotion for ${product.sku}.`, db.settings.activeRole);
  writeDb(db);

  return res.json({ product });
});

app.get('/api/customers', (req, res) => {
  const db = readDb();
  const term = String(req.query.search || '').trim().toLowerCase();

  if (!term) {
    return res.json(db.customers);
  }

  const filtered = db.customers.filter(
    (customer) => customer.name.toLowerCase().includes(term) || customer.phone.includes(term)
  );
  return res.json(filtered);
});

app.get('/api/customers/by-phone/:phone', (req, res) => {
  const db = readDb();
  const phone = normalizePhone(req.params.phone);
  const customer = db.customers.find((entry) => entry.phone === phone);
  if (!customer) {
    return res.status(404).json({ message: 'Customer not found.' });
  }

  return res.json(customer);
});

app.post('/api/customers', (req, res) => {
  const db = readDb();
  const name = String(req.body?.name || '').trim();
  const phone = normalizePhone(req.body?.phone || '');

  if (!name || phone.length < 10) {
    return res.status(400).json({ message: 'Enter a valid name and phone number.' });
  }

  const duplicate = db.customers.some((customer) => customer.phone === phone);
  if (duplicate) {
    return res.status(409).json({ message: 'This phone number already belongs to a member.' });
  }

  const customer = {
    id: uid('cst'),
    name,
    phone,
    points: 0,
    purchaseHistory: []
  };

  db.customers.unshift(customer);
  addAudit(db, `Created loyalty member profile for ${name}.`, db.settings.activeRole);
  writeDb(db);

  return res.status(201).json(customer);
});

app.get('/api/vendors', (_req, res) => {
  const db = readDb();
  res.json(db.vendors);
});

app.get('/api/purchase-orders/drafts', (_req, res) => {
  const db = readDb();
  res.json(draftPurchaseOrders(db));
});

app.post('/api/purchase-orders/drafts/regenerate', (_req, res) => {
  const db = readDb();
  addAudit(db, 'Regenerated draft purchase orders.', db.settings.activeRole);
  writeDb(db);
  res.json(draftPurchaseOrders(db));
});

app.get('/api/audit', (req, res) => {
  const db = readDb();
  const limit = Math.max(1, Number(req.query.limit || 20));
  res.json(db.auditLogs.slice(0, limit));
});

app.get('/api/reports/eod', (_req, res) => {
  const db = readDb();
  res.json(eodReport(db));
});

app.get('/api/reports/shrinkage', (_req, res) => {
  const db = readDb();
  res.json(shrinkageReport(db));
});

app.get('/api/reports/sales-trends', (_req, res) => {
  const db = readDb();
  res.json(db.salesTrend);
});

app.post('/api/checkout', (req, res) => {
  const db = readDb();
  const cart = Array.isArray(req.body?.cart) ? req.body.cart : [];
  const paymentMethod = req.body?.paymentMethod;
  const customerPhone = normalizePhone(req.body?.customerPhone || '');
  const pointsToRedeem = Number(req.body?.pointsToRedeem || 0);
  const userRole = req.body?.userRole;

  if (!['Cash', 'Card', 'Digital'].includes(paymentMethod)) {
    return res.status(400).json({ message: 'Invalid payment method.' });
  }

  if (cart.length === 0) {
    return res.status(400).json({ message: 'Cart is empty.' });
  }

  const normalizedLines = [];
  for (const line of cart) {
    const product = db.products.find((item) => item.id === line.productId);
    if (!product) {
      continue;
    }

    const qty = Math.max(0, Math.min(Math.floor(Number(line.quantity || 0)), product.stock));
    if (qty > 0) {
      normalizedLines.push({ product, quantity: qty });
    }
  }

  if (normalizedLines.length === 0) {
    return res.status(400).json({ message: 'No valid line items available for checkout.' });
  }

  const lineCalculations = normalizedLines.map(({ product, quantity }) => {
    const totals = lineTotals(quantity, product);
    return { product, quantity, ...totals };
  });

  const subtotal = lineCalculations.reduce((sum, line) => sum + line.taxableSubtotal, 0);
  const tax = lineCalculations.reduce((sum, line) => sum + line.tax, 0);
  const discount = lineCalculations.reduce((sum, line) => sum + line.discount, 0);
  const gross = subtotal + tax;

  const customer = customerPhone
    ? db.customers.find((entry) => entry.phone === customerPhone)
    : undefined;

  const requestedPoints = Math.max(0, Math.floor(pointsToRedeem));
  const availablePoints = customer ? customer.points : 0;
  const allowedPoints = Math.min(requestedPoints, availablePoints);
  const maxByTotal = Math.floor(gross / POINT_VALUE);
  const pointsRedeemed = Math.min(allowedPoints, maxByTotal);

  const total = Math.max(0, gross - pointsRedeemed * POINT_VALUE);
  const pointsEarned = Math.floor(total / 12);

  for (const line of lineCalculations) {
    line.product.stock = Math.max(0, line.product.stock - line.quantity);
  }

  const transactionId = uid('tx');
  const nowIso = new Date().toISOString();

  db.payments.unshift({
    id: uid('pay'),
    method: paymentMethod,
    amount: roundMoney(total),
    timestamp: nowIso
  });

  if (customer) {
    customer.points = customer.points - pointsRedeemed + pointsEarned;
    customer.purchaseHistory.unshift({
      id: transactionId,
      date: nowIso,
      total: roundMoney(total),
      pointsEarned
    });
  }

  const currentHour = `${String(new Date().getHours()).padStart(2, '0')}:00`;
  const slot = db.salesTrend.find((item) => item.hour === currentHour);
  if (slot) {
    slot.sales = roundMoney(slot.sales + total);
  } else {
    db.salesTrend.push({ hour: currentHour, sales: roundMoney(total) });
  }

  addAudit(
    db,
    `Checkout ${transactionId} completed (${paymentMethod}) for $${roundMoney(total).toFixed(2)}.`,
    userRole || db.settings.activeRole
  );

  writeDb(db);

  res.json({
    success: true,
    transactionId,
    message: `Checkout complete. Transaction ID: ${transactionId}.`,
    totals: {
      subtotal: roundMoney(subtotal),
      tax: roundMoney(tax),
      discount: roundMoney(discount),
      pointsRedeemed,
      total: roundMoney(total),
      pointsEarned
    },
    bootstrap: bootstrapPayload(db)
  });
});

app.listen(PORT, () => {
  // eslint-disable-next-line no-console
  console.log(`SMS API running on http://localhost:${PORT}`);
});
