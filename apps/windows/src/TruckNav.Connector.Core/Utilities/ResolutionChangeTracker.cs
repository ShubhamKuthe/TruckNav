namespace TruckNav.Connector.Core.Utilities;

public sealed class ResolutionChangeTracker(
    int requiredStableObservations = 3,
    TimeSpan? minimumStableDuration = null)
{
    private readonly TimeSpan _minimumStableDuration = minimumStableDuration ?? TimeSpan.FromMilliseconds(750);
    private int _candidateWidth;
    private int _candidateHeight;
    private int _observations;
    private DateTimeOffset _candidateSince;

    public bool Observe(
        int referenceWidth,
        int referenceHeight,
        int currentWidth,
        int currentHeight,
        DateTimeOffset observedAtUtc)
    {
        if (!CaptureRegionMath.HasMaterialResolutionChange(
                referenceWidth,
                referenceHeight,
                currentWidth,
                currentHeight))
        {
            Reset();
            return false;
        }

        if (_candidateWidth != currentWidth || _candidateHeight != currentHeight)
        {
            _candidateWidth = currentWidth;
            _candidateHeight = currentHeight;
            _observations = 1;
            _candidateSince = observedAtUtc;
            return false;
        }

        _observations++;
        return _observations >= requiredStableObservations &&
               observedAtUtc - _candidateSince >= _minimumStableDuration;
    }

    public void Reset()
    {
        _candidateWidth = 0;
        _candidateHeight = 0;
        _observations = 0;
        _candidateSince = default;
    }
}
