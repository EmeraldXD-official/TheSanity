namespace TheSanity.GlobalNPC.Bosses.DiscordantReligia
{
    public enum ChronoState
    {
        HoveringLaser = 0,
        VoidRiftTeleport = 1,
        TimeSlowBurst = 2,
        Phase2Transition = 3,
        TemporalDash = 4,
        TemporalNova = 5,   // Ultimate: Phase 2, gated by attack counter (not random pool)
        SpiralBarrage = 6,  // Phase 2 pool: rotating bullet-hell laser spiral
        EchoStrike = 7,     // Phase 2 pool: fast triple-blink cone volley
        Oblivion = 8        // Enrage-only: partner is dead, total desperation ultimate
    }

    public enum PlagueState
    {
        AggressiveDash = 0,
        SporeCloudSpread = 1,
        BioLaserSweep = 2,
        Phase2Transition = 3,
        ToxicRain = 4,
        MiasmaNova = 5,     // Ultimate: Phase 2, gated by attack counter (not random pool)
        SporeStorm = 6,     // Phase 2 pool: rotating alternating spore/stinger fan
        VenomLance = 7,     // Phase 2 pool: telegraphed stinger snipes
        Cataclysm = 8,     // Enrage-only: partner is dead, total desperation ultimate
          ContagionBurst = 9 // Enrage-only: 3 rapid interleaved rings + snipe, alternates with Cataclysm
    }
}