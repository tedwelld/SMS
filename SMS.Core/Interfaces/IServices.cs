using SMS.Core.Dtos;

namespace SMS.Core.Interfaces;

public interface ICustomerService : IEntityCrudService<CustomerDto, CreateCustomerRequest>;
public interface ICustomerAccountService : IEntityCrudService<CustomerAccountDto, CreateCustomerAccountRequest>;

public interface IWalletService : IEntityCrudService<WalletDto, CreateWalletRequest>;
public interface IAccessMethodService : IEntityCrudService<AccessMethodDto, CreateAccessMethodRequest>;
public interface INfcCardService : IEntityCrudService<NfcCardDto, CreateNfcCardRequest>;
public interface IQrTokenService : IEntityCrudService<QrTokenDto, CreateQrTokenRequest>;
public interface IStaffUserService : IEntityCrudService<StaffUserDto, CreateStaffUserRequest>;
public interface IAuditLogService : IEntityCrudService<AuditLogDto, CreateAuditLogRequest>;
public interface IMobileAppSessionService : IEntityCrudService<MobileAppSessionDto, CreateMobileAppSessionRequest>;
public interface ISmsNotificationService : IEntityCrudService<SmsNotificationDto, CreateSmsNotificationRequest>;


