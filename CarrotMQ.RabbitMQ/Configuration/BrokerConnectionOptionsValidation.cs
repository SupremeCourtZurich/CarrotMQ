using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;

namespace CarrotMQ.RabbitMQ.Configuration;

/// <summary>
/// Provides validation for <see cref="BrokerConnectionOptions" /> configuration options.
/// </summary>
public sealed class BrokerConnectionOptionsValidation : IValidateOptions<BrokerConnectionOptions>
{
    /// <summary>
    /// Validates the specified <paramref name="options" /> for the <see cref="BrokerConnectionOptions" />.
    /// </summary>
    /// <param name="name">The name of the options, if named. (Not used in this implementation.)</param>
    /// <param name="options">The <see cref="BrokerConnectionOptions" /> to validate.</param>
    /// <returns>A <see cref="ValidateOptionsResult" /> indicating success or failure and containing any validation errors.</returns>
    public ValidateOptionsResult Validate(string? name, BrokerConnectionOptions options)
    {
        List<string> errors = new();

        if (options.BrokerEndPoints == null! || options.BrokerEndPoints.Count == 0)
        {
            errors.Add($"No {nameof(options.BrokerEndPoints)} defined");
        }

        if (string.IsNullOrWhiteSpace(options.VHost))
        {
            errors.Add($"No {nameof(options.VHost)} defined");
        }

        if (string.IsNullOrWhiteSpace(options.UserName))
        {
            errors.Add($"No {nameof(options.UserName)} defined");
        }

        if (string.IsNullOrWhiteSpace(options.ServiceName))
        {
            errors.Add($"No {nameof(options.ServiceName)} defined");
        }

        if (options.PublisherConfirm == null!)
        {
            errors.Add($"{nameof(options.PublisherConfirm)} must be configured");
        }

        return errors.Any() ? ValidateOptionsResult.Fail(errors) : ValidateOptionsResult.Success;
    }
}