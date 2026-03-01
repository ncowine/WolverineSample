namespace TradingAssistant.Application.Indicators;

/// <summary>
/// Volume Profile Calculator: computes volume moving average and relative volume ratio.
/// Used to detect "above average volume" conditions on signal bars.
/// VolumeMA = SMA of volume over N periods.
/// RelativeVolume = CurrentVolume / VolumeMA (>1.0 = above average).
/// </summary>
public class VolumeProfileCalculator
{
    public static readonly VolumeProfileCalculator Instance = new();

    public VolumeProfileResult Calculate(long[] volume, int period = 20)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1, nameof(period));

        var length = volume.Length;
        var volumeMa = new decimal[length];
        var relativeVolume = new decimal[length];

        if (length < period)
            return new VolumeProfileResult(volumeMa, relativeVolume);

        // Compute volume SMA using sliding window
        var sum = 0m;
        for (var i = 0; i < period; i++)
            sum += volume[i];

        volumeMa[period - 1] = sum / period;

        for (var i = period; i < length; i++)
        {
            sum += volume[i] - volume[i - period];
            volumeMa[i] = sum / period;
        }

        // Relative volume = current volume / volume MA
        for (var i = period - 1; i < length; i++)
        {
            relativeVolume[i] = volumeMa[i] == 0 ? 0 : volume[i] / volumeMa[i];
        }

        return new VolumeProfileResult(volumeMa, relativeVolume);
    }

    /// <summary>
    /// Returns true if the volume at the given index is above the threshold relative to MA.
    /// Default threshold = 1.2 (20% above average).
    /// </summary>
    public static bool IsAboveAverage(VolumeProfileResult profile, int index, decimal threshold = 1.2m)
    {
        if (index < 0 || index >= profile.RelativeVolume.Length)
            return false;

        return profile.RelativeVolume[index] >= threshold;
    }
}

public record VolumeProfileResult(decimal[] VolumeMa, decimal[] RelativeVolume);
