using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using OVRLighthouseManager.Activation;
using OVRLighthouseManager.Contracts.Services;
using OVRLighthouseManager.Helpers;
using OVRLighthouseManager.Views;
using Serilog;

namespace OVRLighthouseManager.Services;

public static class Intrusion
{
    public static event Action? OnTurnOn;
    public static bool TurnOn
    {
        set => OnTurnOn?.Invoke();
    }

    public static event Action? OnTurnOff;
    public static bool TurnOff
    {
        set=> OnTurnOff?.Invoke();
    }
}

