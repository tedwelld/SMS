using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Api.Infrastructure;
using SMS.Core.Dtos;
using SMS.Core.Interfaces;
using SMS.Data.DbContext;
using SMS.Data.EntityModels;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/nfc-cards")]
[Authorize(Roles = "Admin,Staff")]
public class NfcCardsController(INfcCardService service, SmsDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NfcCardDto>>> GetAll(CancellationToken cancellationToken) => Ok(await service.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<NfcCardDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var dto = await service.GetByIdAsync(id, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<NfcCardDto>> Create([FromBody] CreateNfcCardRequest request, CancellationToken cancellationToken)
    {
        if (request.WalletId <= 0)
        {
            return BadRequest(new { message = "A valid walletId is required." });
        }

        var cardUid = request.CardUid.Trim();
        if (string.IsNullOrWhiteSpace(cardUid))
        {
            return BadRequest(new { message = "cardUid is required." });
        }

        var wallet = await db.Wallets
            .Include(x => x.CustomerAccount)
            .ThenInclude(x => x.Customer)
            .Include(x => x.NfcCards)
            .FirstOrDefaultAsync(x => x.Id == request.WalletId, cancellationToken);
        if (wallet?.CustomerAccount?.Customer is null)
        {
            return BadRequest(new { message = "Wallet was not found." });
        }

        var normalizedPhone = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? wallet.CustomerAccount.Customer.PhoneNumber
            : UserOnboardingService.NormalizePhone(request.PhoneNumber);
        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            return BadRequest(new { message = "A valid phone number is required." });
        }

        var existingCardForWallet = wallet.NfcCards.OrderByDescending(x => x.Id).FirstOrDefault();
        var existingCardId = existingCardForWallet?.Id ?? 0;

        var cardUidInUse = await db.NfcCards.AnyAsync(
            x => x.CardUid == cardUid
                 && x.Id != existingCardId,
            cancellationToken);
        if (cardUidInUse)
        {
            return BadRequest(new { message = "Card UID is already in use." });
        }

        var phoneInUse = await db.NfcCards.AnyAsync(
            x => x.PhoneNumber == normalizedPhone
                 && x.Id != existingCardId,
            cancellationToken);
        if (phoneInUse)
        {
            return BadRequest(new { message = "Phone number is already linked to another NFC card." });
        }

        NfcCard entity;
        var reissued = false;
        if (existingCardForWallet is null)
        {
            entity = new NfcCard
            {
                WalletId = request.WalletId,
                CardUid = cardUid,
                PhoneNumber = normalizedPhone
            };
            db.NfcCards.Add(entity);
        }
        else
        {
            existingCardForWallet.CardUid = cardUid;
            existingCardForWallet.PhoneNumber = normalizedPhone;
            entity = existingCardForWallet;
            reissued = true;
        }

        await db.SaveChangesAsync(cancellationToken);
        var dto = new NfcCardDto(entity.Id, entity.WalletId, entity.CardUid, entity.PhoneNumber);
        if (reissued)
        {
            return Ok(dto);
        }

        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await service.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}


