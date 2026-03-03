export interface AdminWalletItem {
  id: string;
  customerId: number;
  customerAccountId: number;
  ownerName: string;
  ownerPhone?: string;
  balance: number;
  isFrozen: boolean;
  isActive: boolean;
  status: 'active' | 'suspended' | 'closed';
  cardId?: string;
  createdAt: string;
}

export interface WalletCustomerItem {
  id: number;
  name: string;
  phone: string;
}

export interface StaffUserItem {
  id: number;
  username: string;
  name: string;
  email: string;
  role: string;
  isActive: boolean;
  status?: 'active' | 'inactive' | 'suspended';
  dateCreated: string;
}
