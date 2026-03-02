export interface AdminWalletItem {
  id: string;
  ownerName: string;
  balance: number;
  status: 'active' | 'suspended' | 'closed';
  cardId?: string;
  createdAt: string;
}

export interface StaffUserItem {
  id: number;
  username: string;
  name: string;
  email: string;
  role: string;
  isActive: boolean;
  dateCreated: string;
}
