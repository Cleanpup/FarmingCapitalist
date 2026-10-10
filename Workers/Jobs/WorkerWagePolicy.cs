namespace HireSkilledHelpers.Workers;

internal static class WorkerWagePolicy
{
    public static bool ShouldAttemptPayment(int lastPaidDay, int lastAttemptDay, int today, bool retryUnpaid)
    {
        return lastPaidDay != today && (retryUnpaid || lastAttemptDay != today);
    }
}
