import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth.service';
import { PaymentTrackingRecord } from '../../core/models';
import { SmsStoreService } from '../../core/sms-store.service';

interface DashboardStatPoint {
  key: string;
  label: string;
  value: number;
  format: 'currency' | 'number';
  icon: string;
  route: string;
  description: string;
}

interface TopProductRow {
  productId: string;
  productName: string;
  sku: string;
  unitsSold: number;
  salesAmount: number;
}

type ExportKind = 'sales' | 'ledger' | 'payment' | 'procurement' | 'inventory';
type MeterMetric = {
  label: string;
  value: number;
  target: number;
  suffix: string;
  color: string;
};
type RingMetric = {
  label: string;
  value: number;
  percent: number;
  color: string;
};

@Component({
  selector: 'app-dashboard-page',
  imports: [CommonModule, RouterLink, CurrencyPipe, DatePipe, MatButtonModule, MatCardModule, MatIconModule],
  templateUrl: './dashboard-page.component.html',
  styleUrl: './dashboard-page.component.scss'
})
export class DashboardPageComponent implements OnInit {
  readonly store = inject(SmsStoreService);
  readonly auth = inject(AuthService);

  readonly now = new Date();
  readonly exporting = signal<ExportKind | null>(null);
  readonly canExport = computed(() => this.auth.role === 'admin');
  readonly showAdminVisuals = computed(() => this.auth.role === 'admin');

  readonly salesLedger = computed(() => this.store.paymentHistory().slice(0, this.auth.role === 'admin' ? 80 : 16));

  readonly statSeries = computed<DashboardStatPoint[]>(() => {
    const points: DashboardStatPoint[] = [
      {
        key: 'revenue',
        label: 'End-of-day Revenue',
        value: Math.max(this.store.eodReport().total, 0),
        format: 'currency',
        icon: 'pi pi-wallet',
        route: '/reports',
        description: 'Live total from all payment channels.'
      },
      {
        key: 'transactions',
        label: 'Transactions',
        value: Math.max(this.store.eodReport().transactions, 0),
        format: 'number',
        icon: 'pi pi-shopping-bag',
        route: '/pos',
        description: 'Completed checkouts in the current reporting cycle.'
      },
      {
        key: 'sales-tracked',
        label: 'Tracked Sales Records',
        value: Math.max(this.store.paymentHistory().length, 0),
        format: 'number',
        icon: 'pi pi-database',
        route: '/cash-ups',
        description: 'Sales records available for tracking and receipt reprint.'
      },
      {
        key: 'inventory',
        label: 'Inventory SKUs',
        value: Math.max(this.store.inventory().length, 0),
        format: 'number',
        icon: 'pi pi-box',
        route: '/inventory',
        description: 'Products currently tracked in inventory.'
      },
      {
        key: 'low-stock',
        label: 'Low Stock SKUs',
        value: Math.max(this.store.lowStockItems().length, 0),
        format: 'number',
        icon: 'pi pi-exclamation-triangle',
        route: '/inventory',
        description: 'Items at or below minimum stock threshold.'
      },
      {
        key: 'expiry',
        label: 'Expiry Alerts',
        value: Math.max(this.store.expiringSoonItems().length, 0),
        format: 'number',
        icon: 'pi pi-clock',
        route: '/inventory',
        description: 'Products approaching expiry date soon.'
      }
    ];

    if (this.auth.role === 'admin') {
      points.push(
        {
          key: 'vendors',
          label: 'Vendors',
          value: Math.max(this.store.vendors().length, 0),
          format: 'number',
          icon: 'pi pi-building',
          route: '/procurement',
          description: 'Supplier accounts supporting procurement.'
        },
        {
          key: 'draft-orders',
          label: 'Draft Purchase Orders',
          value: Math.max(this.store.draftPurchaseOrders().length, 0),
          format: 'number',
          icon: 'pi pi-file-edit',
          route: '/procurement',
          description: 'Auto-generated procurement drafts pending review.'
        }
      );
    }

    return points;
  });

  readonly paymentBreakdown = computed(() => {
    const eod = this.store.eodReport();
    return [
      { method: 'Cash', value: eod.cash },
      { method: 'Card', value: eod.card },
      { method: 'EcoCash', value: eod.digital }
    ];
  });

  readonly procurementOrderedUnits = computed(() =>
    this.store.draftPurchaseOrders().reduce(
      (sum, order) => sum + order.lines.reduce((lineSum, line) => lineSum + Math.max(0, line.suggestedOrderQty), 0),
      0
    )
  );

  readonly topSellingProducts = computed<TopProductRow[]>(() => {
    const map = new Map<string, TopProductRow>();
    for (const payment of this.store.paymentHistory()) {
      for (const line of payment.lineItems) {
        const current = map.get(line.productId) ?? {
          productId: line.productId,
          productName: line.productName,
          sku: line.sku,
          unitsSold: 0,
          salesAmount: 0
        };
        current.unitsSold += Math.max(0, line.quantity);
        current.salesAmount += Math.max(0, line.lineTotal);
        map.set(line.productId, current);
      }
    }

    return [...map.values()]
      .sort((a, b) => b.salesAmount - a.salesAmount || b.unitsSold - a.unitsSold || a.productName.localeCompare(b.productName))
      .slice(0, 12);
  });

  readonly totalSalesInLedger = computed(() =>
    this.store.paymentHistory().reduce((sum, payment) => sum + payment.total, 0)
  );

  readonly totalItemsSold = computed(() =>
    this.store.paymentHistory().reduce((sum, payment) => sum + payment.itemCount, 0)
  );

  readonly hourlyVisualRows = computed(() => {
    const trend = this.store.salesTrend().slice(-8);
    const fallbackLabels = ['00', '03', '06', '09', '12', '15', '18', '21'];
    const labels = trend.length > 0
      ? trend.map((slot) => slot.hour.slice(0, 2))
      : fallbackLabels;

    const byHour = new Map<string, { cash: number; card: number; digital: number; tx: number }>();
    for (const payment of this.store.paymentHistory()) {
      const date = new Date(payment.timestamp);
      if (Number.isNaN(date.getTime())) {
        continue;
      }

      const hour = String(date.getHours()).padStart(2, '0');
      const current = byHour.get(hour) ?? { cash: 0, card: 0, digital: 0, tx: 0 };
      if (payment.paymentMethod === 'Cash') {
        current.cash += payment.total;
      } else if (payment.paymentMethod === 'Card') {
        current.card += payment.total;
      } else {
        current.digital += payment.total;
      }
      current.tx += 1;
      byHour.set(hour, current);
    }

    return labels.map((label, index) => {
      const fromTrend = trend[index];
      const fromPayments = byHour.get(label) ?? { cash: 0, card: 0, digital: 0, tx: 0 };
      return {
        label,
        sales: fromTrend ? Number(fromTrend.sales) : fromPayments.cash + fromPayments.card + fromPayments.digital,
        cash: fromPayments.cash,
        card: fromPayments.card,
        digital: fromPayments.digital,
        tx: fromPayments.tx
      };
    });
  });

  readonly lineSalesValues = computed(() => this.hourlyVisualRows().map((row) => row.sales));
  readonly lineCashValues = computed(() => this.hourlyVisualRows().map((row) => row.cash));
  readonly lineCardValues = computed(() => this.hourlyVisualRows().map((row) => row.card));

  readonly ringMetrics = computed<RingMetric[]>(() => {
    const total = Math.max(this.store.eodReport().total, 1);
    const channels = this.paymentBreakdown();
    const colors = ['var(--viz-cash)', 'var(--viz-card)', 'var(--viz-sales)'];
    return channels.map((channel, index) => ({
      label: `${channel.method} Share`,
      value: channel.value,
      percent: Math.max(0, Math.min(100, Math.round((channel.value / total) * 100))),
      color: colors[index % colors.length]
    }));
  });

  readonly meterMetrics = computed<MeterMetric[]>(() => {
    const inventoryCount = Math.max(this.store.inventory().length, 1);
    const healthyInventory = Math.max(0, inventoryCount - this.store.lowStockItems().length);
    const expirySafe = Math.max(0, inventoryCount - this.store.expiringSoonItems().length);
    return [
      {
        label: 'Checkout Throughput',
        value: this.store.eodReport().transactions,
        target: 90,
        suffix: 'tx',
        color: 'var(--viz-sales)'
      },
      {
        label: 'Inventory Health',
        value: healthyInventory,
        target: inventoryCount,
        suffix: 'sku',
        color: 'var(--viz-cash)'
      },
      {
        label: 'Expiry Control',
        value: expirySafe,
        target: inventoryCount,
        suffix: 'sku',
        color: 'var(--viz-card)'
      }
    ];
  });

  readonly groupedBars = computed(() => {
    const rows = this.topSellingProducts().slice(0, 6);
    const max = Math.max(...rows.map((row) => row.salesAmount), 1);
    return rows.map((row) => ({
      label: row.sku.length > 6 ? row.sku.slice(-6) : row.sku,
      value: row.salesAmount,
      height: Math.max(16, Math.round((row.salesAmount / max) * 100))
    }));
  });

  async ngOnInit(): Promise<void> {
    await this.store.refreshPaymentHistory(this.auth.role === 'admin' ? 500 : 120);
  }

  formatStat(point: DashboardStatPoint): string {
    if (point.format === 'currency') {
      return point.value.toLocaleString(undefined, { style: 'currency', currency: 'USD' });
    }

    return point.value.toLocaleString();
  }

  isExporting(kind: ExportKind): boolean {
    return this.exporting() === kind;
  }

  async exportDashboardPdf(kind: ExportKind): Promise<void> {
    if (!this.canExport()) {
      return;
    }

    this.exporting.set(kind);
    try {
      const { jsPDF } = await import('jspdf');
      const doc = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4' });
      const pageWidth = doc.internal.pageSize.getWidth();
      const title = this.exportTitle(kind);

      doc.setFont('helvetica', 'bold');
      doc.setFontSize(14);
      doc.text(title, 14, 14);
      doc.setFont('helvetica', 'normal');
      doc.setFontSize(9);
      doc.text(`Generated: ${new Date().toLocaleString()}`, 14, 20);
      doc.text(`Role: ${this.auth.role}`, 14, 25);

      let y = 33;
      const content = this.exportRows(kind);

      doc.setFillColor(236, 241, 246);
      doc.rect(14, y, pageWidth - 28, 8, 'F');
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(8.5);
      doc.text(content.headers[0], 16, y + 5.2);
      doc.text(content.headers[1], 86, y + 5.2);
      doc.text(content.headers[2], 158, y + 5.2);
      y += 8;

      doc.setFont('helvetica', 'normal');
      doc.setFontSize(8.2);
      for (const row of content.rows) {
        if (y > 280) {
          doc.addPage();
          y = 20;
          doc.setFillColor(236, 241, 246);
          doc.rect(14, y, pageWidth - 28, 8, 'F');
          doc.setFont('helvetica', 'bold');
          doc.setFontSize(8.5);
          doc.text(content.headers[0], 16, y + 5.2);
          doc.text(content.headers[1], 86, y + 5.2);
          doc.text(content.headers[2], 158, y + 5.2);
          y += 8;
          doc.setFont('helvetica', 'normal');
          doc.setFontSize(8.2);
        }

        doc.rect(14, y, pageWidth - 28, 7.2);
        doc.text(row[0], 16, y + 4.8, { maxWidth: 66 });
        doc.text(row[1], 86, y + 4.8, { maxWidth: 68 });
        doc.text(row[2], 158, y + 4.8, { maxWidth: 42 });
        y += 7.2;
      }

      doc.save(`sms-dashboard-${kind}-summary.pdf`);
    } finally {
      this.exporting.set(null);
    }
  }

  private exportTitle(kind: ExportKind): string {
    if (kind === 'sales') {
      return 'Dashboard Sales Summary';
    }
    if (kind === 'ledger') {
      return 'Dashboard Sales Ledger';
    }
    if (kind === 'payment') {
      return 'Dashboard Payment Channel Summary';
    }
    if (kind === 'procurement') {
      return 'Dashboard Procurement Summary';
    }
    return 'Dashboard Inventory Summary';
  }

  private exportRows(kind: ExportKind): { headers: [string, string, string]; rows: string[][] } {
    if (kind === 'sales') {
      return {
        headers: ['Metric', 'Value', 'Notes'],
        rows: [
          ['Gross Sales', this.asCurrency(this.totalSalesInLedger()), 'From all tracked payments'],
          ['Transactions', this.store.eodReport().transactions.toLocaleString(), 'EOD transaction count'],
          ['Items Sold', this.totalItemsSold().toLocaleString(), 'Aggregated quantity'],
          ['Low Stock', this.store.lowStockItems().length.toLocaleString(), 'Current low-stock list'],
          ['Expiry Alerts', this.store.expiringSoonItems().length.toLocaleString(), 'Near expiry inventory']
        ]
      };
    }

    if (kind === 'ledger') {
      const rows = this.salesLedger().slice(0, 50).map((payment: PaymentTrackingRecord) => [
        payment.transactionId,
        `${new Date(payment.timestamp).toLocaleString()} | ${payment.paymentMethod}`,
        this.asCurrency(payment.total)
      ]);

      return {
        headers: ['Transaction', 'Date / Method', 'Total'],
        rows: rows.length > 0 ? rows : [['-', 'No ledger rows available', '-']]
      };
    }

    if (kind === 'payment') {
      const rows = this.paymentBreakdown().map((item) => [
        item.method,
        this.asCurrency(item.value),
        'EOD method total'
      ]);
      rows.push(['TOTAL', this.asCurrency(this.store.eodReport().total), 'All channels combined']);

      return {
        headers: ['Channel', 'Amount', 'Notes'],
        rows
      };
    }

    if (kind === 'procurement') {
      return {
        headers: ['Metric', 'Value', 'Notes'],
        rows: [
          ['Vendors', this.store.vendors().length.toLocaleString(), 'Active suppliers'],
          ['Draft Orders', this.store.draftPurchaseOrders().length.toLocaleString(), 'Pending draft POs'],
          ['Units Ordered', this.procurementOrderedUnits().toLocaleString(), 'Total draft suggested qty'],
          ['Low Stock SKUs', this.store.lowStockItems().length.toLocaleString(), 'Needs replenishment'],
          ['Top Supplier', this.store.vendors()[0]?.name ?? '-', 'First supplier in current dataset']
        ]
      };
    }

    const rows = this.store.inventory().slice(0, 70).map((item) => [
      `${item.name} (${item.sku})`,
      `${item.department} | Qty ${item.stock} | Min ${item.minStock}`,
      item.expiryDate ? `Expiry ${item.expiryDate}` : 'No expiry'
    ]);

    return {
      headers: ['Product', 'Stock Status', 'Expiry'],
      rows: rows.length > 0 ? rows : [['-', 'No inventory items available', '-']]
    };
  }

  private asCurrency(value: number): string {
    return value.toLocaleString(undefined, { style: 'currency', currency: 'USD' });
  }

  ringDashOffset(percent: number): number {
    const circumference = 2 * Math.PI * 36;
    return circumference * (1 - percent / 100);
  }

  meterPercent(metric: MeterMetric): number {
    return Math.max(0, Math.min(100, Math.round((metric.value / Math.max(metric.target, 1)) * 100)));
  }

  polylinePoints(values: number[], width = 760, height = 210): string {
    const safe = values.length > 0 ? values : [0, 0];
    const max = Math.max(...safe, 1);
    const n = safe.length;
    const step = n > 1 ? width / (n - 1) : width;
    return safe
      .map((value, index) => {
        const x = index * step;
        const y = height - (value / max) * (height - 12) - 6;
        return `${x.toFixed(2)},${y.toFixed(2)}`;
      })
      .join(' ');
  }

  areaPath(values: number[], width = 760, height = 210): string {
    const points = this.polylinePoints(values, width, height).split(' ');
    if (points.length === 0) {
      return '';
    }
    const first = points[0].split(',')[0];
    const last = points[points.length - 1].split(',')[0];
    return `M ${first} ${height} L ${points.join(' L ')} L ${last} ${height} Z`;
  }
}
