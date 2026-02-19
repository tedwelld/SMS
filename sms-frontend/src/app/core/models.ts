export type UserRole = 'Store Manager' | 'Cashier' | 'Stock Clerk';

export type Department = 'Grocery' | 'Dairy' | 'Electronics' | 'Household' | 'Produce';

export type PromotionType = 'none' | 'discount' | 'bogo';

export interface Promotion {
  type: PromotionType;
  value: number;
}

export interface Product {
  id: string;
  name: string;
  sku: string;
  department: Department;
  price: number;
  stock: number;
  minStock: number;
  taxRate: number;
  staple: boolean;
  expiryDate?: string;
  promo: Promotion;
  physicalCount: number;
}

export interface CartItem {
  productId: string;
  name: string;
  sku: string;
  price: number;
  quantity: number;
  taxRate: number;
  promo: Promotion;
}

export interface PurchaseRecord {
  id: string;
  date: string;
  total: number;
  pointsEarned: number;
}

export interface CustomerProfile {
  id: string;
  name: string;
  phone: string;
  points: number;
  purchaseHistory: PurchaseRecord[];
}

export interface Vendor {
  id: string;
  name: string;
  departments: Department[];
  leadTimeDays: number;
  contact: string;
  email: string;
}

export type PaymentMethod = 'Cash' | 'Card' | 'Digital';

export interface PaymentRecord {
  id: string;
  method: PaymentMethod;
  amount: number;
  timestamp: string;
}

export interface AuditEntry {
  id: string;
  timestamp: string;
  userRole: UserRole;
  action: string;
}

export interface PurchaseOrderLine {
  productId: string;
  name: string;
  sku: string;
  currentStock: number;
  suggestedOrderQty: number;
}

export interface DraftPurchaseOrder {
  id: string;
  vendorId: string;
  vendorName: string;
  createdAt: string;
  lines: PurchaseOrderLine[];
}

export interface CartTotals {
  subtotal: number;
  tax: number;
  discount: number;
  pointsRedeemed: number;
  total: number;
  pointsEarned: number;
}

export interface EodReport {
  cash: number;
  card: number;
  digital: number;
  total: number;
  transactions: number;
}

export interface ShrinkageReportRow {
  product: string;
  sku: string;
  systemStock: number;
  physicalCount: number;
  variance: number;
}

export interface SalesTrendPoint {
  hour: string;
  sales: number;
}

export interface AppSettings {
  roles: UserRole[];
  activeRole: UserRole;
  offlineMode: boolean;
}

export interface ReportsPayload {
  eod: EodReport;
  shrinkage: ShrinkageReportRow[];
}

export interface BootstrapPayload {
  settings: AppSettings;
  products: Product[];
  customers: CustomerProfile[];
  vendors: Vendor[];
  draftPurchaseOrders: DraftPurchaseOrder[];
  salesTrend: SalesTrendPoint[];
  reports: ReportsPayload;
  auditLogs: AuditEntry[];
}

export interface CheckoutLineItem {
  productId: string;
  quantity: number;
}

export interface CheckoutRequest {
  cart: CheckoutLineItem[];
  paymentMethod: PaymentMethod;
  customerPhone: string;
  pointsToRedeem: number;
  userRole: UserRole;
}

export interface CheckoutResponse {
  success: boolean;
  transactionId: string;
  message: string;
  totals: CartTotals;
  bootstrap: BootstrapPayload;
}

export interface OperationResult {
  success: boolean;
  message: string;
}
