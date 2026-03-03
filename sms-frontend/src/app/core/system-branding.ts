export interface SystemBranding {
  name: string;
  shortName: string;
  email: string;
  phone: string;
  addressLine1: string;
  addressLine2: string;
  logoPath: string;
}

export const SYSTEM_BRANDING: SystemBranding = {
  name: 'Supermarket Management System',
  shortName: 'SMS',
  email: 'admin@sms.local',
  phone: '+263774700574',
  addressLine1: '3281 Hlalanikuhle Ext-Hwange',
  addressLine2: 'Dete place',
  logoPath: '/branding/sms-logo.svg'
};

export const SYSTEM_BRANDING_FULL_ADDRESS = `${SYSTEM_BRANDING.addressLine1}, ${SYSTEM_BRANDING.addressLine2}`;
