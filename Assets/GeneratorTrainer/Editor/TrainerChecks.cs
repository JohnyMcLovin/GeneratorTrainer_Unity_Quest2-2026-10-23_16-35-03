using System;
using UnityEditor;
using UnityEngine;
namespace GeneratorTrainer.Editor
{
    public static class TrainerChecks
    {
        static void Expect(bool ok, string text) { if (!ok) throw new Exception("Trainer check failed: " + text); }
        static void Do(TrainingState s, string action, int expected, int index = 0) { s.Act(action,index); Expect(s.step == expected, action + " -> step " + expected); }
        static TrainingState BeforeStart()
        {
            var s = new TrainingState();
            s.Act("Start"); Expect(!s.running && s.step == 0,"premature start blocked");
            Do(s,"PPE",1); Do(s,"Exhaust",2); Do(s,"Estop",3); Do(s,"Door",4); Do(s,"ReadPlate",5); Do(s,"Inspect",6);
            Do(s,"Key",7); Do(s,"Mode",8); Do(s,"Stop",9); Do(s,"Cover",10); Do(s,"Inspect",11);
            Do(s,"Breaker",11,0); Expect(!s.McbOK,"wrong breaker not credited"); Do(s,"Breaker",12,1); Expect(s.McbOK,"all breakers on"); Do(s,"Cover",13);
            Do(s,"Inspect",14); Do(s,"Fuel",14); Expect(!s.pouring,"fuel cap guard"); Do(s,"Cap",15); Do(s,"Fuel",15);
            s.Tick(4.9f); Expect(!s.fuelOK && s.step==15,"fuel timer"); s.Tick(.2f); Expect(s.fuelOK && s.step==16,"fuel completion"); Expect(!s.FuelLampOK,"cap needed for fuel green"); Do(s,"Cap",17);
            Do(s,"Charger",18); Do(s,"Charger",18); Expect(s.chargerConnected,"early disconnect blocked");
            s.Tick(7.9f); Expect(!s.batteryOK,"battery timer"); s.Tick(.2f); Expect(s.batteryOK && s.step==19,"charge completion");
            Expect(!s.Ready,"charger must be disconnected"); Do(s,"Charger",20); Expect(s.Ready,"all start guards clear"); Do(s,"Inspect",21);
            return s;
        }
        [MenuItem("Tools/Generator Trainer/4 - Run Procedure Checks")]
        public static void Run()
        {
            var s = BeforeStart(); Do(s,"Start",22); Expect(s.running,"engine starts");
            Do(s,"Verify",23); Do(s,"Stop",24); Expect(!s.running,"engine stops"); Do(s,"Mode",25); Do(s,"Key",26); Do(s,"Door",27);
            Expect(s.Complete && !s.doorOpen && !s.keyInserted && s.mode==0 && !s.running,"safe shutdown final state");
            s = BeforeStart(); Do(s,"Start",22); s.Act("Estop"); Expect(!s.running && s.estopPressed,"E-STOP stops a running engine");
            s = BeforeStart(); Do(s,"Start",22); s.Act("Stop"); Expect(!s.running && s.stopPressed,"STOP interrupts immediately");
            s = BeforeStart(); s.exhaustSafe = false; s.Act("Start"); Expect(!s.running,"unsafe exhaust blocks start");
            s = BeforeStart(); s.ppe = false; s.Act("Start"); Expect(!s.running,"PPE blocks start");
            s = BeforeStart(); s.breakers[3] = false; s.Act("Start"); Expect(!s.running,"tripped breaker blocks start");
            Debug.Log("PASS: 27-step training flow, timers, sequencing, start interlocks, emergency stop and safe shutdown.");
        }
    }
}
