using System.Collections.Generic;

namespace GeneratorTrainer.Multiplayer
{
    // Short, readable names for the generator's TrainerControl hit boxes, shown under the crosshair.
    // Keys are the object names created from Source/TrainerModel.json.
    public static class ControlLabels
    {
        static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            { "DoorHandleHit", "Door handle" },
            { "ReadPlateHit", "Instruction plate" },
            { "ExhaustHit", "Exhaust pipe" },
            { "DisplayHit", "Display" },
            { "MCBCheck", "MCB lamp" },
            { "FUELCheck", "FUEL lamp" },
            { "BATTERYCheck", "BATTERY lamp" },
            { "KeyHit", "Key switch" },
            { "KeyPropHit", "Key" },
            { "ModeHit", "Mode selector" },
            { "StopHit", "STOP button" },
            { "StartHit", "START button" },
            { "EstopHit", "E-STOP" },
            { "BreakerHit0", "Breaker 1" },
            { "BreakerHit1", "Breaker 2" },
            { "BreakerHit2", "Breaker 3" },
            { "BreakerHit3", "Breaker 4" },
            { "CoverHit", "MCB cover" },
            { "ChargerHit", "BAT. CHARGE socket" },
            { "ChargerPropHit", "Charger plug" },
            { "CapHit", "Fuel cap" },
            { "CanHit", "Diesel can" },
            { "BoardPPEHit", "PPE button" },
            { "BoardCheckHit", "CHECK button" },
            { "BoardResetHit", "RESET button" },
        };

        public static string For(TrainerControl control)
        {
            if (control == null) return "";
            string label;
            return Names.TryGetValue(control.name, out label) ? label : control.action;
        }
    }
}
