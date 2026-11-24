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
        public int step;
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
            "A6  Inspect the three red fault lights (CHECK)",
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
        public bool Applies(int s)
        {
            if (s >= 9 && s <= 12) return fault == Fault.MCB;
            if (s >= 13 && s <= 16) return fault == Fault.Fuel;
            if (s >= 17 && s <= 19) return fault == Fault.Battery;
            return true;
        }

        public int RequiredCount { get { int n = 0; for (int i = 0; i < Instructions.Length; i++) if (Applies(i)) n++; return n; } }

        // 1-based position of the current step among the required steps.
        public int Position { get { int n = 0; for (int i = 0; i <= Math.Min(step, Instructions.Length - 1); i++) if (Applies(i)) n++; return n; } }

        public string FaultLamp { get { return fault == Fault.MCB ? "MCB" : fault == Fault.Fuel ? "FUEL" : "BATTERY"; } }

        void Next(string message)
        {
            step++;
            while (!Complete && !Applies(step)) step++;
            feedback = message;
        }
        bool Need(int expected) { if (step == expected) return true; feedback = "Next: " + Current; return false; }

        // Performs an action and classifies it: correct, wrong, emergency, started or after_end.
        // A wrong action does not change the procedure; it is counted and adds a time penalty.
        public string Act(string action, int index = 0)
        {
            if (Complete) { feedback = "Exercise complete. RESET starts a new attempt."; return "after_end"; }
            int oldStep = step;
            bool wasRunning = running;
            Apply(action, index);
            if (step > oldStep) return "correct";
            if (action == "Estop" && oldStep > 2) return "emergency";
            if (action == "Stop" && wasRunning && !running) return "emergency";
            if (action == "Fuel" && pouring) return "started";
            wrongCount++;
            penaltySeconds += PenaltyPerWrong;
            feedback += "  Penalty +" + PenaltyPerWrong.ToString("0") + " s.";
            return "wrong";
        }

        void Apply(string action, int index)
        {
            // Emergency intervention always takes precedence over the guided sequence.
            if (action == "Estop" && step > 2)
            {
                estopPressed = !estopPressed;
                if (estopPressed) { running = false; feedback = "Emergency stop applied. RESET to restart this guided exercise."; }
                else feedback = "E-STOP released. RESET if the exercise was interrupted.";
                return;
            }
            if (action == "Stop" && running && step != 23)
            { running = false; stopPressed = true; feedback = "Engine stopped. RESET to restart the interrupted exercise."; return; }
            switch (action)
            {
                case "PPE": if (Need(0)) { ppe = true; Next("Eye and ear protection confirmed."); } break;
                case "Exhaust": if (Need(1)) { exhaustSafe = true; Next("Exhaust points to the marked outdoor vent."); } break;
                case "Estop": if (Need(2)) { estopPressed = false; Next("Side E-STOP released."); } break;
                case "Door":
                    if (step == 3) { doorOpen = true; Next("Read the plate on the inside of the door."); }
                    else if (step == 26 && !running && mode == 0 && !keyInserted) { doorOpen = false; Next("Safe shutdown complete."); }
                    else feedback = "Keep the door open until the shutdown steps are finished.";
                    break;
                case "ReadPlate": if (Need(4) && doorOpen) Next("Operating instructions read."); break;
                case "Inspect":
                    if (step == 5) Next(FaultLamp + " is red. The other lamps are green.");
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
                    if (step == 7 && keyInserted) { mode = 1; Next("MAN selected. Release the panel STOP."); }
                    else if (step == 24 && !running) { mode = 0; Next("Mode OFF. Remove the key."); }
                    else feedback = "MAN is required for this exercise; AUTO is not used.";
                    break;
                case "Stop":
                    if (step == 8) { stopPressed = false; Next("Panel STOP released."); }
                    else if (step == 23) { running = false; stopPressed = true; Next("Engine stopped. Select OFF."); }
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
