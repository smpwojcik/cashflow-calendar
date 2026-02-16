namespace CashFlowCalendar.Data;

public static class Forecast
{
    public record DailyBalance(DateOnly Date, decimal DayNet, decimal ProjectedBalance);

    public static IEnumerable<DailyBalance> Build(
        decimal currentBalance,
        DateOnly balanceAsOf,
        DateOnly startDate,
        int days,
        IReadOnlyList<Txn> upcomingTxns)
    {
        // Normalize: treat currentBalance as of balanceAsOf, and begin projecting from startDate.
        var txnsByDate = upcomingTxns
            .GroupBy(t => t.Date)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

        var balance = currentBalance;

        // If balanceAsOf is after startDate, we still start at startDate using currentBalance as "known".
        for (var i = 0; i < days; i++)
        {
            var date = startDate.AddDays(i);
            txnsByDate.TryGetValue(date, out var dayNet);
            balance += dayNet;
            yield return new DailyBalance(date, dayNet, balance);
        }
    }
}
