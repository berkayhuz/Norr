// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable
namespace Norr.PerformanceMonitor.Abstractions;

public interface IProcessProbe
{
    /// Process içindeki açık socket sayısı (tüm protokoller)
    int? GetSocketCount();


    /// Açık dosya tanıtıcı (fd/handle) sayısı
    int? GetOpenFileDescriptorCount();


    /// (Linux) İsteğe göre context switch toplamları – süreç başlangıcından beri
    (long? Voluntary, long? NonVoluntary) GetContextSwitchTotals();
}
