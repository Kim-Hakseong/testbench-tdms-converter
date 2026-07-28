namespace Tdms.Core;

/// <summary>Base class for every error the TDMS reader raises on purpose.</summary>
public class TdmsException : Exception
{
    /// <summary>Creates the exception with a default message.</summary>
    public TdmsException()
        : base("TDMS error.")
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Human readable description.</param>
    public TdmsException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Human readable description.</param>
    /// <param name="innerException">Underlying cause.</param>
    public TdmsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The bytes are not a valid TDMS stream (bad tag, impossible offsets, truncated header).</summary>
public sealed class TdmsFormatException : TdmsException
{
    /// <summary>Creates the exception with a default message.</summary>
    public TdmsFormatException()
        : base("Malformed TDMS file.")
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Human readable description.</param>
    public TdmsFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Human readable description.</param>
    /// <param name="innerException">Underlying cause.</param>
    public TdmsFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// The file is valid TDMS but uses a feature this reader deliberately does not implement.
/// Raised instead of producing numbers that would be silently wrong.
/// </summary>
public sealed class TdmsUnsupportedFeatureException : TdmsException
{
    /// <summary>Creates the exception with a default message.</summary>
    public TdmsUnsupportedFeatureException()
        : base("Unsupported TDMS feature.")
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Human readable description.</param>
    public TdmsUnsupportedFeatureException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Human readable description.</param>
    /// <param name="innerException">Underlying cause.</param>
    public TdmsUnsupportedFeatureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
