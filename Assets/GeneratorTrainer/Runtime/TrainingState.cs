using System;
namespace GeneratorTrainer
{
    // The one fault that is present in a run. Only its repair phase (C, D or E) is required.
    public enum Fault { MCB, Fuel, Battery }

    // Pure training logic, independent of Unity rendering and XR packages.
    [Serializable]
    public sealed class TrainingState
    {
        // Seconds added to the run time for every wrong action (soft fail: logged, never ends the run).
        public static float PenaltyPerWrong = 10f;

        public Fault fault = Fault.MCB;
        public int trippedBreaker = 1;              // 0..3, only used for the MCB fault
        public float elapsed, penaltySeconds;       // run timer and accumulated penalty, in seconds
        public int wrongCount;

        // After a wrong action the same control ignores presses for a moment (no extra penalty).
        public static float LockoutSeconds = 2f;
        public string lockedAction = "";
        public float lockedUntil;

        // step = first step that is not done yet. done[] allows the any-order groups from docs/steps.csv.
        public int step;
        public bool[] done = new bool[27];
        bool recovery, emergency;   // set by Apply for the action being judged
        public bool ppe, exhaustSafe, estopPressed = true, doorOpen, keyInserted;
        public bool stopPressed = true, coverOpen, capRemoved, chargerConnected;
        public bool fuelOK, batteryOK, running, pouring;
        public int mode; // 0 OFF, 1 MAN, 2 AUTO
        public bool[] breakers = { true, false, true, true };
        public float fuelTime, chargeTime;
        public string feedback = "Confirm eye and ear protection on the PPE button.";
        public static readonly string[] Instructions = {
            "A1  Confirm eye and ear protection (PPE)",
            "A2  Turn exhaust toward OUTDOORS",
            "A3  Twist / release the side E-STOP",
            "A4  Open the control-panel door",
            "A5  Read and touch the inside instructions",
            "A6  Inspect the warning lights (CHECK)",
            "B1  Insert the key",
            "B2  Turn mode selector to MAN",
            "B3  Release the red panel STOP button",
            "C1  Lift the MCB cover",
            "C2  Find the down breaker (CHECK)",
            "C3  Flip the tripped breaker up to ON",
            "C4  Close the MCB cover; MCB is green",
            "D1  Inspect DIESEL / NO SMOKING sign (CHECK)",
            "D2  Remove the diesel filler cap",
            "D3  Fill with diesel using the fuel can",
            "D4  Replace and tighten the cap",
            "E1  Connect charger to BAT. CHARGE",
            "E2  Wait for the battery light to turn green",
            "E3  Disconnect the charger",
            "F1  Check all faults and START light green",
            "F2  Press the green START button",
            "F3  Check the display and engine sound",
            "G1  Press the red STOP button",
            "G2  Turn mode selector to OFF",
            "G3  Remove the key",
            "G4  Close the control-panel door"
        };
        public bool Complete { get { return step >= Instructions.Length; } }
        public bool McbOK { get { foreach (bool b in breakers) if (!b) return false; return true; } }
        public bool FuelLampOK { get { return fuelOK && !capRemoved; } }
        public bool Ready { get { return ppe && exhaustSafe && !estopPressed && keyInserted && mode == 1 && !stopPressed && McbOK && !coverOpen && FuelLampOK && batteryOK && !chargerConnected; } }
        public string Current { get { return Complete ? "COMPLETE - safe shutdown verified" : Instructions[step]; } }
        public float TotalTime { get { return elapsed + penaltySeconds; } }

        // A fresh run with exactly one fault. The other two systems start healthy.
        public static TrainingState NewRun(Fault fault, int trippedBreaker = 1)
        {
            var s = new TrainingState();
            s.fault = fault;
            s.trippedBreaker = Math.Max(0, Math.Min(3, trippedBreaker));
            for (int i = 0; i < s.breakers.Length; i++) s.breakers[i] = !(fault == Fault.MCB && i == s.trippedBreaker);
            s.fuelOK = fault != Fault.Fuel;
            s.batteryOK = fault != Fault.Battery;
            return s;
        }

        public static TrainingState NewRandomRun(Random rng)
        {
            return NewRun((Fault)rng.Next(3), rng.Next(4));
        }

        // Steps C1-C4 (9-12), D1-D4 (13-16) and E1-E3 (17-19) only apply to their own fault.
        public bool Applies(int s) { return InProcedure(s, (int)fault); }

        // Whether a step belongs to the procedure for a fault (0 MCB, 1 fuel, 2 battery; -1 = no fault chosen,
        // which leaves out all three repair phases).
        public static bool InProcedure(int s, int faultIndex)
        {
            if (s >= 9 && s <= 12) return faultIndex == (int)Fault.MCB;
            if (s >= 13 && s <= 16) return faultIndex == (int)Fault.Fuel;
            if (s >= 17 && s <= 19) return faultIndex == (int)Fault.Battery;
            return true;
        }

        // The fault code the Expert has chosen from the Technician's description (-1 = not chosen yet).
        // The Expert never sees the real fault; the manual follows this choice.
        public int expertDiagnosis = -1;
        public bool DiagnosisCorrect { get { return expertDiagnosis == (int)fault; } }

        public int RequiredCount { get { int n = 0; for (int i = 0; i < Instructions.Length; i++) if (Applies(i)) n++; return n; } }

        // 1-based position of the current step among the required steps.
        public int Position { get { int n = 0; for (int i = 0; i <= Math.Min(step, Instructions.Length - 1); i++) if (Applies(i)) n++; return n; } }

        public string FaultLamp { get { return LampOf(fault); } }

        // Fault codes, as listed in the Expert's manual.
        public static string CodeOf(Fault f) { return f == Fault.MCB ? "F-101" : f == Fault.Fuel ? "F-201" : "F-301"; }
        public static string TitleOf(Fault f) { return f == Fault.MCB ? "MCB TRIPPED" : f == Fault.Fuel ? "FUEL LEVEL LOW" : "BATTERY LOW"; }
        public static string LampOf(Fault f) { return f == Fault.MCB ? "MCB" : f == Fault.Fuel ? "FUEL" : "BATTERY"; }
        public static string DetailOf(Fault f)
        {
            return f == Fault.MCB ? "The main circuit breaker protection has tripped. One of the four breakers behind the MCB cover is OFF (lever down)."
                 : f == Fault.Fuel ? "The fuel tank is empty. The tank must be refilled with diesel only through the fuel filler."
                 : "The starter battery is discharged. It must be charged from the BAT. CHARGE / JUMP START socket.";
        }
        public string FaultCode { get { return CodeOf(fault); } }
        public string FaultTitle { get { return TitleOf(fault); } }
        public string FaultDetail { get { return DetailOf(fault); } }

        // The Expert's manual: one entry per step, same order as Instructions (from docs/steps.csv).
        public static readonly string[] Manual = {
            "Put on safety glasses and ear protection. The blue stickers on the unit show both are required. Confirm with PPE on the training board.",
            "Check that the exhaust pipe on top points outdoors (toward the vent), never into the room.",
            "Check the side E-STOP is not pressed in. If it is, twist it to release.",
            "Open the control panel door using the yellow handle.",
            "Read the Operating Instructions plate on the inside of the door.",
            "Look at the three warning lights and note which are red: MCB (top), FUEL (middle), BATTERY (bottom). Confirm with CHECK.",
            "Insert the key into the key switch. The ring lights green.",
            "Turn the mode switch to MAN.",
            "Make sure the red STOP button on the panel is released (pulled out).",
            "Lift the MCB cover.",
            "Find the breaker in the down (OFF) position: that is the tripped one. Confirm with CHECK.",
            "Flip the tripped breaker up to ON. The other breakers must not be touched.",
            "Close the MCB cover. The MCB light turns green.",
            "Go to the fuel filler with the D / DIESEL ONLY sign. No smoking. Confirm with CHECK.",
            "Remove the fuel cap.",
            "Fill with diesel only, using the DIESEL can. Filling takes about 5 seconds.",
            "Put the cap back and tighten it. The FUEL light turns green.",
            "Connect the charger cable to the BAT. CHARGE / JUMP START socket.",
            "Wait until the BATTERY light turns green (about 8 seconds).",
            "Disconnect the charger cable.",
            "Check that all three warning lights and the light beside START are green. If any is red, go back to that part. Confirm with CHECK.",
            "Press the green START button. Pressing START with a red light is a wrong action.",
            "Check that the display turns on with no fault and the engine is running. Confirm by selecting the display.",
            "Press the red STOP button.",
            "Turn the mode switch to OFF.",
            "Remove the key.",
            "Close the control panel door.",
        };

        // The Technician's last action and how it was judged (for the Expert screen and, later, the network).
        public string lastAction = "", lastResult = "";

        // Feedback for the Technician in collaborative mode: the result only, without procedure hints.
        public string TechnicianFeedback
        {
            get
            {
                if (Complete) return "Exercise complete.";
                if (pouring) return feedback;                       // filling progress
                if (chargerConnected && step == 18) return feedback; // charging progress
                switch (lastResult)
                {
                    case "correct": return "OK.";
                    case "wrong": return "That was not right. Penalty +" + PenaltyPerWrong.ToString("0") + " s. Ask the Expert.";
                    case "emergency": return feedback;
                    case "started": return feedback;
                    case "recovery": return feedback;
                    case "locked": return feedback;
                    default: return "Ask the Expert what to do first.";
                }
            }
        }

        // Steps that may be done in any order once the group is reached (docs/steps.csv order_mode "any"):
        // A1-A3, then A5-A6 after A4, then B2-B3 after B1.
        static readonly int[][] AnyOrder = { new[] { 0, 1, 2 }, new[] { 4, 5 }, new[] { 7, 8 } };

        // True if step s may be done now: it is the current step, or in the same any-order group and not done yet.
        bool At(int s)
        {
            if (Complete || done[s] || !Applies(s)) return false;
            if (s == step) return true;
            foreach (var g in AnyOrder)
                if (Array.IndexOf(g, step) >= 0 && Array.IndexOf(g, s) >= 0) return true;
            return false;
        }
        bool Need(int s) { if (At(s)) return true; feedback = "Next: " + Current; return false; }
        void Done(int s, string message)
        {
            done[s] = true;
            while (!Complete && (done[step] || !Applies(step))) step++;
            feedback = message;
        }
        void Next(string message) { Done(step, message); }

        // The engine was stopped before F3 was confirmed: go back to F2, so START can be pressed again.
        void BackToStart()
        {
            if (step != 22) return;
            done[21] = false;
            step = 21;
        }

        // Performs an action and classifies it: correct, wrong, emergency, started or after_end.
        // A wrong action does not change the procedure; it is counted and adds a time penalty.
        public string Act(string action, int index = 0)
        {
            string result = Judge(action, index);
            lastAction = action;
            lastResult = result;
            return result;
        }

        string Judge(string action, int index)
        {
            if (Complete) { feedback = "Exercise complete. RESET starts a new attempt."; return "after_end"; }
            string control = action == "Breaker" ? "Breaker" + index : action;
            if (control == lockedAction && elapsed < lockedUntil)
            {
                feedback = "That control is locked for a moment after a wrong action.";
                return "locked";
            }
            int doneBefore = DoneCount;
            recovery = emergency = false;
            Apply(action, index);
            if (emergency) return "emergency";
            if (recovery) return "recovery";
            if (DoneCount > doneBefore) return "correct";
            if (action == "Fuel" && pouring) return "started";
            wrongCount++;
            penaltySeconds += PenaltyPerWrong;
            // Safety controls (E-STOP, STOP) are never locked, so they can always be released or used.
            if (action != "Estop" && action != "Stop") { lockedAction = control; lockedUntil = elapsed + LockoutSeconds; }
            feedback += "  Penalty +" + PenaltyPerWrong.ToString("0") + " s.";
            return "wrong";
        }

        int DoneCount { get { int n = 0; foreach (bool d in done) if (d) n++; return n; } }

        void Apply(string action, int index)
        {
            // E-STOP after A3. Pressing it with the engine running is a real emergency (no penalty). Pressing it
            // with nothing running is a wrong action. Releasing it again is a recovery (no penalty).
            if (action == "Estop" && done[2])
            {
                if (estopPressed) { estopPressed = false; recovery = true; feedback = "E-STOP released."; return; }
                estopPressed = true;
                if (running)
                {
                    running = false; emergency = true; BackToStart();
                    feedback = "Emergency stop: engine stopped. Release the E-STOP before starting again.";
                }
                else feedback = "E-STOP pressed although nothing was running. Release it.";
                return;
            }
            // Panel STOP while the engine runs (except the normal G1 press) stops it at once.
            if (action == "Stop" && running && step != 23)
            {
                running = false; stopPressed = true; emergency = true; BackToStart();
                feedback = "Engine stopped early. Release STOP, then press START again.";
                return;
            }
            switch (action)
            {
                case "PPE": if (Need(0)) { ppe = true; Done(0, "Eye and ear protection confirmed."); } break;
                case "Exhaust": if (Need(1)) { exhaustSafe = true; Done(1, "Exhaust points to the marked outdoor vent."); } break;
                case "Estop": if (Need(2)) { estopPressed = false; Done(2, "Side E-STOP released."); } break;
                case "Door":
                    if (step == 3) { doorOpen = true; Next("Read the plate on the inside of the door."); }
                    else if (step == 26 && estopPressed) feedback = "Release the E-STOP before closing the door.";
                    else if (step == 26 && !running && mode == 0 && !keyInserted) { doorOpen = false; Next("Safe shutdown complete."); }
                    else feedback = "Keep the door open until the shutdown steps are finished.";
                    break;
                case "ReadPlate": if (Need(4) && doorOpen) Done(4, "Operating instructions read."); break;
                case "Inspect":
                    if (At(5)) Done(5, FaultLamp + " is red. The other lamps are green.");
                    else if (step == 10 && coverOpen) Next("Breaker " + (trippedBreaker + 1) + " is down / OFF. Flip it up.");
                    else if (step == 13) Next("Diesel only. No smoking. Open the fuel cap.");
                    else if (step == 20 && Ready) Next("All lamps are green. START is ready.");
                    else feedback = Current;
                    break;
                case "Key":
                    if (step == 6 && doorOpen) { keyInserted = true; Next("Key ring illuminated green."); }
                    else if (step == 25 && mode == 0 && !running) { keyInserted = false; Next("Key removed and returned to the shelf."); }
                    else feedback = "Insert key at B1; remove only after mode is OFF at G3.";
                    break;
                case "Mode":
                    if (At(7) && keyInserted) { mode = 1; Done(7, "MAN selected."); }
                    else if (step == 24 && !running) { mode = 0; Next("Mode OFF. Remove the key."); }
                    else feedback = "MAN is required for this exercise; AUTO is not used.";
                    break;
                case "Stop":
                    if (At(8)) { stopPressed = false; Done(8, "Panel STOP released."); }
                    else if (step == 23) { running = false; stopPressed = true; Next("Engine stopped. Select OFF."); }
                    else if (stopPressed && !running && done[8] && step < 23) { stopPressed = false; recovery = true; feedback = "Panel STOP released."; }
                    else feedback = "STOP is used at B3 and G1.";
                    break;
                case "Cover":
                    if (step == 9) { coverOpen = true; Next("Find the breaker in the down position."); }
                    else if (step == 12 && McbOK) { coverOpen = false; Next("Cover closed. MCB indicator green."); }
                    else feedback = Current;
                    break;
                case "Breaker":
                    if (Need(11) && coverOpen && index == trippedBreaker) { breakers[index] = true; Next("Tripped breaker reset to ON."); }
                    else if (step == 11) feedback = "That breaker is already ON. Find the one that is down.";
                    break;
                case "Cap":
                    if (step == 14 && !running) { capRemoved = true; Next("Cap removed. Use the diesel can."); }
                    else if (step == 16 && fuelOK) { capRemoved = false; Next("Cap tightened. FUEL indicator green."); }
                    else feedback = Current;
                    break;
                case "Fuel": if (Need(15) && capRemoved && !running) { pouring = true; feedback = "Filling with diesel..."; } break;
                case "Charger":
                    if (step == 17 && !running) { chargerConnected = true; Next("Charger connected. Wait for the green light."); }
                    else if (step == 19 && batteryOK) { chargerConnected = false; Next("Charger disconnected."); }
                    else feedback = Current;
                    break;
                case "Start":
                    if (Need(21) && Ready) { running = true; Next("Engine running. Check the display."); }
                    else if (!Ready) feedback = "START blocked: finish safety checks and clear faults.";
                    break;
                case "Verify": if (Need(22) && running) Next("Display: 400 V / 50 Hz / NO FAULT. Engine sound active."); break;
            }
        }
        public void Tick(float dt)
        {
            if (!Complete) elapsed += Math.Max(0, dt);
            if (pouring && step == 15 && capRemoved && !running)
            {
                fuelTime += Math.Max(0, dt);
                feedback = "Filling diesel: " + Math.Min(100, (int)(fuelTime / 5 * 100)) + "%";
                if (fuelTime >= 5) { fuelOK = true; pouring = false; Next("Tank filled. Refit and tighten the cap."); }
            }
            if (chargerConnected && step == 18 && !running)
            {
                chargeTime += Math.Max(0, dt);
                feedback = "Battery charging: " + Math.Min(100, (int)(chargeTime / 8 * 100)) + "%";
                if (chargeTime >= 8) { batteryOK = true; Next("BATTERY green. Disconnect the charger."); }
            }
        }
    }
}
