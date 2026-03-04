namespace SMS.Data.Enums;

public enum PosPaymentMethod
{
    Cash = 0,
    Card = 1,
    EcoCash = 2,
    // Legacy DB value retained for backward compatibility; equivalent to EcoCash.
    Digital = EcoCash
}

