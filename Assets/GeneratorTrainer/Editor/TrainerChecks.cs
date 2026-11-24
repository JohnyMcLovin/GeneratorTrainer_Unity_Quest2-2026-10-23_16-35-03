using System;
using UnityEditor;
using UnityEngine;
namespace GeneratorTrainer.Editor
{
    public static class TrainerChecks
    {
        static void Expect(bool ok, string text) { if (!ok) throw new Exception("Trainer check failed: " + text); }
        static void Do(TrainingState s, string action, int expected, int index = 0)
        {
            string r = s.Act(action, index);
            Expect(s.step == expected, action + " -> step " + expected + " (got " + s.step + ", " + r + ")");
        }
        static void Wrong(TrainingState s, string action, int index = 0)
        {
            int step = s.step, wrong = s.wrongCount; float pen = s.penaltySeconds;
            Expect(s.Act(action, index) == "wrong", action + " at step " + step + " is a wrong action");
            Expect(s.step == step, "wrong action does not advance");
            Expect(s.wrongCount == wrong + 1 && Math.Abs(s.penaltySeconds - pen - TrainingState.PenaltyPerWrong) < 1e-4f,
                "wrong action adds one penalty");
        }

        // A1-B3, shared by every fault. Ends on the first step of the fault's repair phase.
        static TrainingState Prepare(Fault fault, int tripped = 2)
        {
            var s = TrainingState.NewRun(fault, tripped);
            Expect(s.wrongCount == 0 && s.penaltySeconds == 0 && s.elapsed == 0, "clean run");
            Expect(s.McbOK == (fault != Fault.MCB) && s.fuelOK == (fault != Fault.Fuel) && s.batteryOK == (fault != Fault.Battery),
                fault + ": only this system is faulty");
            Wrong(s, "Start"); Expect(!s.running, "premature start blocked");
            Do(s, "PPE", 1); Do(s, "Exhaust", 2); Do(s, "Estop", 3); Do(s, "Door", 4); Do(s, "ReadPlate", 5);
            Do(s, "Inspect", 6); Expect(s.feedback.StartsWith(s.FaultLamp), "A6 names the red lamp");
            Do(s, "Key", 7); Do(s, "Mode", 8);
            int first = fault == Fault.MCB ? 9 : fault == Fault.Fuel ? 13 : 17;
            Do(s, "Stop", first);
            return s;
        }

        static TrainingState Repaired(Fault fault)
        {
            var s = Prepare(fault);
            if (fault == Fault.MCB)
            {
                Do(s, "Cover", 10); Do(s, "Inspect", 11);
                Wrong(s, "Breaker", 0); Expect(!s.McbOK, "wrong breaker not credited");
                Do(s, "Breaker", 12, 2); Expect(s.McbOK, "all breakers on");
                Do(s, "Cover", 20);
            }
            else if (fault == Fault.Fuel)
            {
                Wrong(s, "Cover");
                Do(s, "Inspect", 14); Wrong(s, "Fuel"); Expect(!s.pouring, "fuel cap guard");
                Do(s, "Cap", 15); Expect(s.Act("Fuel") == "started" && s.pouring, "pouring starts");
                s.Tick(4.9f); Expect(!s.fuelOK && s.step == 15, "fuel timer");
                s.Tick(.2f); Expect(s.fuelOK && s.step == 16, "fuel completion");
                Expect(!s.FuelLampOK, "cap needed for fuel green");
                Do(s, "Cap", 20);
            }
            else
            {
                Wrong(s, "Cap");
                Do(s, "Charger", 18); Wrong(s, "Charger"); Expect(s.chargerConnected, "early disconnect blocked");
                s.Tick(7.9f); Expect(!s.batteryOK, "battery timer");
                s.Tick(.2f); Expect(s.batteryOK && s.step == 19, "charge completion");
                Expect(!s.Ready, "charger must be disconnected");
                Do(s, "Charger", 20);
            }
            Expect(s.Ready, fault + ": all start guards clear");
            Do(s, "Inspect", 21);
            return s;
        }

        [MenuItem("Tools/Generator Trainer/4 - Run Procedure Checks")]
        public static void Run()
        {
            foreach (Fault fault in new[] { Fault.MCB, Fault.Fuel, Fault.Battery })
            {
                var s = Repaired(fault);
                Do(s, "Start", 22); Expect(s.running, "engine starts");
                Do(s, "Verify", 23); Do(s, "Stop", 24); Expect(!s.running, "engine stops");
                Do(s, "Mode", 25); Do(s, "Key", 26); Do(s, "Door", 27);
                Expect(s.Complete && !s.doorOpen && !s.keyInserted && s.mode == 0 && !s.running, fault + ": safe shutdown final state");
                Expect(s.Position == s.RequiredCount, fault + ": every required step was visited");
                float before = s.elapsed; s.Tick(5); Expect(s.elapsed == before, "timer stops when the run is complete");
                Expect(s.Act("Door") == "after_end" && s.wrongCount == (fault == Fault.MCB ? 2 : 3), fault + ": no penalty after the end");

                s = Repaired(fault); Do(s, "Start", 22); Expect(s.Act("Estop") == "emergency" && !s.running && s.estopPressed, "E-STOP stops a running engine");
                s = Repaired(fault); Do(s, "Start", 22); Expect(s.Act("Stop") == "emergency" && !s.running && s.stopPressed, "STOP interrupts immediately");
                s = Repaired(fault); s.exhaustSafe = false; s.Act("Start"); Expect(!s.running, "unsafe exhaust blocks start");
                s = Repaired(fault); s.ppe = false; s.Act("Start"); Expect(!s.running, "PPE blocks start");
                s = Repaired(fault); s.breakers[3] = false; s.Act("Start"); Expect(!s.running, "tripped breaker blocks start");
            }
            Expect(TrainingState.NewRun(Fault.MCB).RequiredCount == 20 && TrainingState.NewRun(Fault.Fuel).RequiredCount == 20
                && TrainingState.NewRun(Fault.Battery).RequiredCount == 19, "required step counts");

            var timed = TrainingState.NewRun(Fault.Battery); timed.Tick(3); timed.Tick(-1);
            Expect(Math.Abs(timed.elapsed - 3) < 1e-4f, "run timer counts up and ignores negative time");

            var rng = new System.Random(1); var seen = new bool[3]; var breakers = new bool[4];
            for (int i = 0; i < 200; i++)
            {
                var r = TrainingState.NewRandomRun(rng); seen[(int)r.fault] = true;
                if (r.fault == Fault.MCB) breakers[r.trippedBreaker] = true;
            }
            Expect(seen[0] && seen[1] && seen[2], "random runs pick every fault");
            Expect(breakers[0] && breakers[1] && breakers[2] && breakers[3], "random MCB runs pick every breaker");

            Debug.Log("PASS: one fault per run (MCB, fuel, battery), step skipping, run timer, wrong-action penalties, "
                + "start interlocks, emergency stop and safe shutdown.");
        }
    }
}
