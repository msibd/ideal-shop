namespace Optical.Domain.Enums;

/// <summary>
/// Why goods were swapped. Recorded as a fixed list rather than free text so the shop can
/// see what keeps coming back — a run of PoorFit or WrongPrescription is worth acting on.
/// </summary>
public enum ExchangeReason
{
    WrongPrescription = 1,
    PoorFit = 2,
    DamagedOrDefective = 3,
    WrongProductGiven = 4,
    CustomerChangedMind = 5,
    Other = 6
}
