using Cronos;

using KeyProvider.Signing;

using Microsoft.Extensions.Options;

namespace KeyProvider.KeyRotation;

internal sealed class RotationScheduleValidator(
    IOptions<KeyRotationOptions> rotationOptions,
    IOptions<RotationRetryOptions> retryOptions,
    IOptions<SigningOptions> signingOptions,
    TimeProvider timeProvider)
    : IValidateOptions<RotationScheduleOptions>
{
    public ValidateOptionsResult Validate(string? name, RotationScheduleOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Expression))
        {
            return ValidateOptionsResult.Fail("KeyRotation:Schedule:Expression is required.");
        }

        if (!CronExpression.TryParse(options.Expression, out CronExpression? expression))
        {
            return ValidateOptionsResult.Fail($"'{options.Expression}' is not a valid cron expression.");
        }

        (TimeSpan minGap, TimeSpan maxGap) = GetGaps(expression);
        if (maxGap == TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail($"'{options.Expression}' must fire repeatedly.");
        }

        RotationRetryOptions retry = retryOptions.Value;
        TimeSpan worstCaseRetry = (retry.MaxRetryAttempts + 1) * retry.AttemptTimeout
                                  + retry.MaxDelay * retry.MaxRetryAttempts;
        TimeSpan failedRoundAndRetry = options.FailureRetryDelay + 2 * worstCaseRetry;
        if (minGap <= failedRoundAndRetry)
        {
            return ValidateOptionsResult.Fail(
                $"The smallest gap between schedule slots ({minGap}) must be longer than " +
                $"KeyRotation:Schedule:FailureRetryDelay ({options.FailureRetryDelay}) plus twice the worst-case " +
                $"retry duration ({2 * worstCaseRetry}).");
        }

        TimeSpan tokenLifetime = signingOptions.Value.TokenLifetime;
        TimeSpan publishDelay = signingOptions.Value.PublishDelay;

        TimeSpan lifespan = rotationOptions.Value.Lifespan;
        if (lifespan <= maxGap + failedRoundAndRetry + retry.AttemptTimeout + tokenLifetime + publishDelay)
        {
            return ValidateOptionsResult.Fail(
                $"KeyRotation:Lifespan ({lifespan}) must be longer than the largest gap between schedule slots " +
                $"({maxGap}) plus KeyRotation:Schedule:FailureRetryDelay ({options.FailureRetryDelay}) plus twice the " +
                $"worst-case retry duration ({2 * worstCaseRetry}) plus KeyRotation:Retry:AttemptTimeout " +
                $"({retry.AttemptTimeout}) plus Signing:TokenLifetime ({tokenLifetime}) plus " +
                $"Signing:PublishDelay ({publishDelay}).");
        }

        return ValidateOptionsResult.Success;
    }

    private (TimeSpan Min, TimeSpan Max) GetGaps(CronExpression expression)
    {
        TimeSpan minGap = TimeSpan.MaxValue;
        TimeSpan maxGap = TimeSpan.Zero;
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset end = now.AddYears(1);
        DateTimeOffset? previous = expression.GetNextOccurrence(now, TimeZoneInfo.Utc);

        while (previous is not null && previous <= end)
        {
            DateTimeOffset? next = expression.GetNextOccurrence(previous.Value, TimeZoneInfo.Utc);
            TimeSpan? gap = next - previous;
            // Stryker disable once Equality : equivalent, assigning an equal gap changes nothing
            if (gap > maxGap)
            {
                maxGap = gap.Value;
            }

            // Stryker disable once Equality : equivalent, assigning an equal gap changes nothing
            if (gap < minGap)
            {
                minGap = gap.Value;
            }

            previous = next;
        }

        return (minGap, maxGap);
    }
}
