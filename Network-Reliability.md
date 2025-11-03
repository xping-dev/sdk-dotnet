# Network Reliability Metrics (Direct signal, not proxy)

## Network Metrics

```csharp
public class NetworkMetrics
{
    public int? LatencyMs { get; set; }           // Ping to Xping API
    public bool? IsOnline { get; set; }           // Network availability
    public string? ConnectionType { get; set; }   // WiFi, Ethernet, Cellular, Unknown
    public int? PacketLossPercent { get; set; }   // If measurable
}
```

**Benefits:**

- Direct measure of network quality (not inferred from location)
- Privacy-friendly
- Actionable: "Test fails when latency > 500ms"

## Privacy-First Approach

1. Let Users Opt-In

```json
{
  "Xping": {
    "CollectNetworkMetrics": true
  }
}
```