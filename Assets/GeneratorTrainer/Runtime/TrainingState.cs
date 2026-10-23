using System;
namespace GeneratorTrainer
{
    // Pure training logic, independent of Unity rendering and XR packages.
    [Serializable]
    public sealed class TrainingState
    {
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
            "C3  Flip breaker 2 up to ON",
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
        void Next(string message) { step++; feedback = message; }
        bool Need(int expected) { if (step == expected) return true; feedback = "Next: " + Current; return false; }
        public void Act(string action, int index = 0)
        {
            if (Complete) { feedback = "Exercise complete. RESET starts a new attempt."; return; }
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
                    if (step == 5) Next("MCB, FUEL and BATTERY are red.");
                    else if (step == 10 && coverOpen) Next("Breaker 2 is down / OFF. Flip it up.");
                    else if (step == 13) Next("Diesel only. No smoking. Open the fuel cap.");
                    else if (step == 20 && Ready) Next("All three faults are cleared. START is green.");
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
                    if (Need(11) && coverOpen && index == 1) { breakers[index] = true; Next("Tripped breaker reset to ON."); }
                    else if (step == 11) feedback = "That breaker is already ON. Reset breaker 2.";
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
