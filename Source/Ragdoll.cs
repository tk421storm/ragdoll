using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TKS_Ragdoll
{

    [DefOf]
    public static class Defs
    {
        public static RulePackDef Event_Tossed;
    }

    public class TKS_Ragdoll : Mod
    {

        TKS_RagdollSettings settings;

        public static void DebugMessage(string message)
        {
            if (LoadedModManager.GetMod<TKS_Ragdoll>().GetSettings<TKS_RagdollSettings>().debugPrint)
            {
                Log.Message(message);
            }
        }

        public TKS_Ragdoll(ModContentPack content) : base(content)
        {
            this.settings = GetSettings<TKS_RagdollSettings>();

            Harmony harmony = new Harmony("TKS_Ragdoll");
            //Harmony.DEBUG = true;
            harmony.PatchAll();
            //Harmony.DEBUG = false;
            Log.Message($"TKS_Ragdoll: Patching finished");

        }

        private string editBufferFloat;

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            listingStandard.CheckboxLabeled("TKSDebugPrint".Translate(), ref settings.debugPrint);
            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "TKSRagdollName".Translate();
        }
    }

    public class TKS_RagdollSettings : ModSettings
    {
        public bool debugPrint = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref debugPrint, "debugPrint");

            base.ExposeData();
        }
    }

    public static class TKSUtils
    {

        private static Dictionary<int, Map> allMapsByPawn = new Dictionary<int, Map>();

        public static MapComponent_Toss Tossers(this Thing th)
        {
            return th.Map.Tossers();
        }

        public static MapComponent_Toss Tossers(this Map map)
        {
            return map.GetComponent<MapComponent_Toss>();
        }

        public static void AddMap(Pawn pawn)
        {
            if (!allMapsByPawn.ContainsKey(pawn.thingIDNumber))
            {
                allMapsByPawn.Add(pawn.thingIDNumber, pawn.Map);
            }
        }

        public static void StopStun(this StunHandler stunner)
        {
            Traverse.Create(stunner).Field("stunTicksLeft").SetValue(0);
            //stunner.stunTicksLeft = 0;
        }

        public static Map GetMap(int ID)
        {
            if (allMapsByPawn.ContainsKey(ID))
            {
                return allMapsByPawn[ID];
            }

            return null;
        }
    }


    public class MapComponent_Toss : MapComponent
    {

        private List<int> tossingIDs;

        private List<Thing> tossingThings;

        public MapComponent_Toss(Map map) : base(map)
        {
            this.tossingIDs = new List<int>();

            this.tossingThings = new List<Thing>();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref this.tossingIDs, "tossing", LookMode.Value);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (this.tossingIDs.Count == 0)
            {
                return;
            }

            //TKS_Ragdoll.DebugMessage("map " + this.map.ToString() + " currently ticking " + this.tossingThings.Count.ToString() + " Things ");// (does not include pawns): " + String.Join(", ", this.tossingThings));

            List<Thing> doneTossing = new List<Thing>();
            foreach (Thing thing in this.tossingThings)
            {
                CompTossable tossable = thing.TryGetComp<CompTossable>();
                if (tossable == null || tossable.tossing == false)
                {
                    TKS_Ragdoll.DebugMessage("map " + this.map.ToString() + " toss of "+thing.ThingID+" complete.");
                    doneTossing.Add(thing);
                    continue;
                }

                //override to force the thing to tick
                thing.Tick();
            }

            foreach (Thing thing in doneTossing)
            {
                this.tossingThings.Remove(thing);
                this.tossingIDs.Remove(thing.thingIDNumber);
            }
        }
        public void TossThis(Thing thing)
        {
            TKS_Ragdoll.DebugMessage("map " + this.map.ToString() + " beginning toss of "+thing.ThingID);

            this.tossingIDs.Add(thing.thingIDNumber);
            this.tossingThings.Add(thing);
        }


        public bool ObstructionCheck(IntVec3 point)
        {
            return (this.map.pathing.Normal.pathGrid.Walkable(point) && GenGrid.InBounds(point, map));
        }

        public override void FinalizeInit()
        {
            
            base.FinalizeInit();

            if (this.tossingIDs == null)
            {
                this.tossingIDs = new List<int>();
            }

            //dont bother trying to restart tosses
            /*
            if (this.tossingIDs.Count!=0)
            {
                TKS_Ragdoll.DebugMessage("map " + this.map.ToString() + " finalize init: attempting to continue tossing " + this.tossingIDs.Count.ToString() + " Things ");// (does not include pawns): " + String.Join(", ", this.tossingIDs));

                List<int> removeThese = new List<int>();

                foreach (int thingID in this.tossingIDs)
                {

                    bool found = false;
                    foreach(Thing thing in this.map.GetDirectlyHeldThings().ToList<Thing>())
                    {
                        if (thing.thingIDNumber == thingID)
                        {
                            this.tossingThings.Add(thing);
                            found = true;
                        }

                        if (found)
                        {
                            break;
                        }
                    }

                    if (!found)
                    {
                        Log.Warning("unable to find thing with id " + thingID.ToString() + ", just means it wont be tossed correctly.");
                        removeThese.Add(thingID);
                    }
                }

                foreach (int thingID in removeThese)
                {
                    this.tossingIDs.Remove(thingID);
                }
                    
            }
            */
        }
    }

    public class CompTossable : ThingComp
    {

        private CompProperties_Tossable Props
        {
            get
            {
                return (CompProperties_Tossable)this.props;
            }
        }

        private float parabola(int length, float xValue)
        {
            return (-(1.0f / (float)length) * xValue * xValue) + xValue;
        }

        private void DrawLine(IntVec3 start, IntVec3 dest)
        {
            int x0 = start.x;
            int y0 = start.z;
            int x1 = dest.x;
            int y1 = dest.z;

            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy, e2; /* error value e_xy */

            List<IntVec3> line = new List<IntVec3>();

            for (; ; )
            {  /* loop */
                IntVec3 pixel = new IntVec3(x0, 0, y0);
                line.Add(pixel);
                if (x0 == x1 && y0 == y1) break;
                e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; } /* e_xy+e_x > 0 */
                if (e2 <= dx) { err += dx; y0 += sy; } /* e_xy+e_y < 0 */
            }

            this.line = line;

            //TKS_Ragdoll.DebugMessage("created list of positions between start and dest: "+String.Join(", ", this.line));
        }

        public void CalculateTossFromExplosion(Explosion explosion, Thing thing)
        {
            if (this.tosserId == explosion.thingIDNumber)
            {
                TKS_Ragdoll.DebugMessage(explosion.ThingID + " is already tossing "+thing.ThingID);
                return;
            } else
            {
                this.tosserId = explosion.thingIDNumber;
            }
            float damage = explosion.GetDamageAmountAt(thing.Position) / 10f;

            TKS_Ragdoll.DebugMessage(thing.ThingID + " recieved " + damage.ToString() + " damage at position "+thing.Position+" from explosion at position "+explosion.Position);

            if (damage == 0.0f)
            {
                TKS_Ragdoll.DebugMessage(explosion.ThingID + " no toss since no damage");
                return;
            }

            float adjust = 1.0f;

            if (!explosion.damageFalloff)
            {
                //adjust value at cell
                adjust = (explosion.radius - thing.Position.DistanceTo(explosion.Position)) / explosion.radius;
                TKS_Ragdoll.DebugMessage(explosion.ThingID + " no falloff, adjusting toss distance by " + adjust.ToString());

            }

            Vector3 angle = new Vector3();

            if (thing.Position.ToVector3() != explosion.Position.ToVector3())
            {
                angle = thing.Position.ToVector3() - explosion.Position.ToVector3();
            }
            else
            {
                //toss randomly
                IntVec3 above = new IntVec3(Mathf.RoundToInt(UnityEngine.Random.Range(-1, 1)), 0, Mathf.RoundToInt(UnityEngine.Random.Range(-1, 1)));
                IntVec3 up = thing.Position + above;
                angle = thing.Position.ToVector3() - up.ToVector3();
            }

            angle = angle.normalized;

            TKS_Ragdoll.DebugMessage(thing.ThingID + " normalized explosion angle: " + angle.ToString());

            Vector3 tossVector = angle * (damage * adjust);

            TKS_Ragdoll.DebugMessage(thing.ThingID + " toss Vector: " + tossVector.ToString());

            Vector3 tossPoint =  tossVector + thing.Position.ToVector3();
            IntVec3 tossPointVec3 = tossPoint.ToIntVec3();

            TKS_Ragdoll.DebugMessage(thing.ThingID + " tossing to " + tossPointVec3.ToString());

            LocalTargetInfo tossDest = new LocalTargetInfo(tossPointVec3);

            //stop pawns from moving

            if (thing is Pawn)
            {
                Pawn pawn = (Pawn)thing;
                pawn.pather.StopDead();
                //__instance.pather.StartPath(tossDest, PathEndMode.OnCell);
            }

            this.StartToss(thing.Map.uniqueID, thing.Position, tossPointVec3, explosion.instigator);

        }

        public void StartToss(int mapID, IntVec3 start, IntVec3 dest, Thing instigator)
        {


            ThingWithComps thing = this.parent;

            if (thing == null)
            {
                Log.Warning("attempt begin toss with null object");
                return;
            }

            Map map = thing.Map;

            if (map == null)
            {
                //try through ID
                map = TKSUtils.GetMap(mapID);

                if (map == null)
                {
                    Log.Warning("attempt to being toss without map");
                    return;
                }
                
            }

            this.tossing = true;
            this.progress = 0;
            this.tweenProgress = 1;
            this.destination = dest;
            this.hitSomething = null;
            this.start = start;
            this.startTick = Find.TickManager.TicksGame;
            this.tweenPoint = start.ToVector3();

            DrawLine(this.start, this.destination);

            TKS_Ragdoll.DebugMessage(thing.ThingID + " checking toss arc for obstructions");

            MapComponent_Toss tossComponent = map.GetComponent<MapComponent_Toss>();

            //TKS_Ragdoll.DebugMessage(thing.ThingID + " using map component "+tossComponent.ToString());

            int i = 0;
            foreach(IntVec3 item in this.line)
            {
                IntVec3 point = this.line[i];

                //TKS_Ragdoll.DebugMessage(thing.ThingID + " checking point "+point);

                if (!tossComponent.ObstructionCheck(point))
                {
                    TKS_Ragdoll.DebugMessage(thing.ThingID + " stopping toss at impassable/out of bounds point "+point.ToString() +" (current toss arc "+String.Join(", ", this.line)+", i: "+i.ToString()+")");

                    if (i<=1)
                    {
                        TKS_Ragdoll.DebugMessage(thing.ThingID + " impassable/out of bounds point cancels toss");
                        this.tossing = false;
                        return;
                    }

                    this.line = this.line.GetRange(0, i);
                    this.destination = this.line.Last();

                    TKS_Ragdoll.DebugMessage(thing.ThingID + " new toss arc: " + String.Join(", ", this.line));

                    //check for impact
                    Building edifice = point.GetEdifice(tossComponent.map);

                        if (edifice != null)
                    {
                        this.hitSomething = edifice;
                    }

                    break;
                }
                i += 1;
            }

            if (line[0]==line.Last())
            {
                TKS_Ragdoll.DebugMessage(thing.ThingID + " cacnel toss as first point is same as last");
                StopToss();
                return;

            }

            //add height (doesnt render anything by default)
            /*
            int y = 0;
            List<IntVec3> lineWithHeight = new List<IntVec3>();
            foreach (IntVec3 linePoint in line)
            {
                float height = parabola(line.Count()-1, y);
                //TKS_Ragdoll.DebugMessage("adding " + height.ToString() + " to point " + y.ToString());
                lineWithHeight.Add(new IntVec3(linePoint.x, (int)Math.Round(height), linePoint.z));
                y += 1;
            }
            this.line = lineWithHeight;
            */

            TKS_Ragdoll.DebugMessage(thing.ThingID + " toss begun from " + start.ToString() + " to " + dest.ToString()+ ": ["+String.Join(", ", this.line)+"]");

            this.tossAngle = -(dest.ToVector3() - start.ToVector3()).normalized;

            if (!(thing is Pawn))
            {
                map.Tossers().TossThis(thing);
            }
            else
            {
                //try to stop them from what they're doing
                Pawn pawn = (Pawn)thing;

                if (pawn?.jobs?.curDriver != null)
                {
                    pawn.jobs.curDriver.EndJobWith(JobCondition.InterruptForced);
                }
                Pawn_StanceTracker stances = pawn.stances;

                if (stances != null)
                {
                    StunHandler stunner = stances.stunner;
                    if (stunner != null)
                    {
                        if (stunner.Stunned)
                        {
                            stunner.StopStun();
                        }
                    }
                }
            }

            if (instigator != null)
            {
                Find.BattleLog.Add(new BattleLogEntry_Event(this.parent, Defs.Event_Tossed, instigator));
            }

        }

        public void StopToss()
        {
            this.tossing = false;
            this.tosserId = 0;

            ThingWithComps thing = this.parent;

            if (thing == null || thing.Destroyed) { return; }

            int stunTicks = 0;

            //do impact if any
            if (this.hitSomething != null)
            {
                TKS_Ragdoll.DebugMessage(thing.ThingID+" hits "+this.hitSomething.ThingID);

                //clear it 
                this.hitSomething = null;
                stunTicks = 125;
            }

            //just pawns for now
            if (!(thing is Pawn))
            {
                return; 
            }

            Pawn pawn = thing as Pawn;

            Pawn_StanceTracker stances = pawn.stances;
            if (stances != null)
            {
                StunHandler stunner = stances.stunner;
                if (stunner != null)
                {
                    if (!pawn.Downed && stunTicks!=0)
                    {
                        TKS_Ragdoll.DebugMessage(thing.ThingID + " stunned for " + stunTicks.ToString() + " ticks");
                        stunner.StunFor(stunTicks, thing, false, true);
                    }
                }
            }

            //restart pather (?)
            pawn.pather.Notify_Teleported_Int();

        }

        public override void CompTick()
        {
            //only pawns tick here by default so we use the mapcomponent tick to force all things to tick
            //TKS_Ragdoll.DebugMessage(thing.ThingID + " is ticking");

            base.CompTick();
            if (!this.tossing)
            {
                return;
            }

            ThingWithComps thing = this.parent;

            if (thing == null || thing.Destroyed)
            {
                TKS_Ragdoll.DebugMessage(thing.ThingID + " toss ended due to null");
                StopToss();
                return;
            }

            TKS_Ragdoll.DebugMessage(thing.ThingID + " is being tossed");

            int tossRate = 5;

            IntVec3 currentPoint = thing.Position;

            if ((Find.TickManager.TicksGame-this.startTick) % tossRate == 0)
            {

                IntVec3 newPoint;
                //push along
                if (this.progress >= (this.line.Count() - 1))
                {
                    newPoint = this.line.Last();
                }
                else
                {
                    newPoint = this.line[progress];
                }

                MapComponent_Toss tossComponent = thing.Map.GetComponent<MapComponent_Toss>();

                if (!tossComponent.ObstructionCheck(newPoint))
                {
                    Log.Warning("Cancelling toss to " + newPoint.ToString() + " due to obstruction, out of bounds");
                    StopToss();
                    return;
                }
                TKS_Ragdoll.DebugMessage(thing.ThingID + " setting to "+newPoint.ToString());

                thing.Position = newPoint;

                this.progress += 1;

                this.tweenProgress = 1;

                if (newPoint==this.destination || (newPoint.x == this.destination.x && newPoint.z == this.destination.z))
                {
                    TKS_Ragdoll.DebugMessage(thing.ThingID + " toss complete");
                    thing.Position = new IntVec3(thing.Position.x, 0, thing.Position.z);
                    StopToss();
                    return;
                }

            } else
            {
                //tween non-pawn objects
                if (!(thing is Pawn))
                {
                    float percentage = (float)this.tweenProgress / (float)tossRate;
                    //TKS_Ragdoll.DebugMessage(thing.ThingID + " tween is "+percentage.ToString()+"% done");
                    Vector3 tweenPoint = percentage*this.tossAngle;
                    TKS_Ragdoll.DebugMessage(thing.ThingID + " tween point "+tweenPoint);
                    tweenPoint = currentPoint.ToVector3() - tweenPoint;
                    TKS_Ragdoll.DebugMessage(thing.ThingID + " tweening to "+tweenPoint);

                    this.tweenPoint = tweenPoint;
                    //thing.Draw();
                    if (thing != null && !thing.Destroyed)
                    {
                        thing.DrawAt(tweenPoint);
                    }

                    this.tweenProgress += 1;
                    
                }
            }


        }

        public bool tossing = false;

        public IntVec3 destination;

        public Vector3 tweenPoint;

        private int tosserId = 0;

        private Vector3 tossAngle;

        private IntVec3 start;

        private int progress;

        private int tweenProgress;

        private int startTick;

        private List<IntVec3> line;

        private Thing hitSomething = null;


    }

    public class CompProperties_Tossable : CompProperties
    {
        public CompProperties_Tossable()
        {
            this.compClass = typeof(CompTossable);
        }

        public float adjustIntensity;
    }

    [HarmonyPatch(typeof(Pawn_JobTracker))]
    public static class Pawn_JobTracker_patches
    {
        [HarmonyPatch(typeof(Pawn_JobTracker), "TryFindAndStartJob")]
        [HarmonyPrefix]
        public static bool TryFindAndStartJob_Prefix(Pawn_JobTracker __instance)
        {
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();

            CompTossable tossable = pawn.TryGetComp<CompTossable>();

            if (tossable != null && tossable.tossing)
            {
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Pawn))]
    public static class Pawn_patches
    {
        /*
        [HarmonyPatch(typeof(Pawn), "DrawAt")]
        [HarmonyPrefix]
        public static bool DrawAt_Prefix(Vector3 drawLoc, bool flip, Pawn __instance)
        {
            if (__instance.TryGetComp<CompTossable>() != null && __instance.TryGetComp<CompTossable>().tossing)
            {
                __instance.Drawer.DrawAt(__instance.TryGetComp<CompTossable>().tweenPoint+new Vector3(0, .2f, 0));
                return false;
            }
            return true;
        }
        */

        [HarmonyPatch(typeof(Pawn), "Kill")]
        [HarmonyPrefix]
        public static bool Kill_Prefix(DamageInfo? dinfo, Hediff exactCulprit, Pawn __instance)
        {
            TKSUtils.AddMap(__instance);

            return true;
        }


        [HarmonyPatch(typeof(Pawn), "MakeCorpse", new Type[] { typeof(Building_Grave), typeof(Building_Bed) })]
        [HarmonyPostfix]
        public static void MakeCorpse_postfix(Building_Grave assignedGrave, Building_Bed currentBed, ref Pawn __instance, ref Corpse __result)
        {

            CompTossable tossable = __instance.TryGetComp<CompTossable>();

            if (tossable == null)
            {
                return;
            }

            if (tossable.tossing)
            {
                TKS_Ragdoll.DebugMessage(__instance.Name+" died during toss, attempting to toss corpse to toss destination "+tossable.destination.ToString());
                CompTossable tossableCorpse = __result.TryGetComp<CompTossable>();

                if (tossableCorpse != null)
                {
                    IntVec3 startPoint = __instance.Position;
                    IntVec3 destination = tossable.destination;
                    tossableCorpse.StartToss(__instance.thingIDNumber, startPoint, destination, null);

                    //__instance.DeSpawn(DestroyMode.Vanish);

                } else
                {
                    TKS_Ragdoll.DebugMessage(__instance.Name + " corpse is not tossable");
                }

            }
        }
    }

    [HarmonyPatch(typeof(ThingDefGenerator_Corpses))]
    public static class Corpses_patches
    {
        [HarmonyPatch(typeof(ThingDefGenerator_Corpses), "ImpliedCorpseDefs")]
        [HarmonyPostfix]
        static IEnumerable<ThingDef> Postfix(IEnumerable<ThingDef> thingDefs)
        {
            foreach (ThingDef def in thingDefs)
            {
                def.comps.Add(new CompProperties_Tossable());
                TKS_Ragdoll.DebugMessage("created corpse for "+def.defName+" with comps "+ String.Join(", ", def.comps));
                yield return def;
            }
        }
    }

    /*
    [HarmonyPatch(typeof(Explosion))]
    public static class Explosion_patches
    {
        [HarmonyPatch(typeof(Explosion), "AffectCell")]
        [HarmonyPostfix]

        public static void AffectCell(IntVec3 c, Explosion __instance)
        {
            if (!c.InBounds(__instance.Map))
            {
                return;
            }
            if (__instance.excludeRadius > 0f && (float)c.DistanceToSquared(__instance.Position) < __instance.excludeRadius * __instance.excludeRadius)
            {
                return;
            }

            foreach (Thing thing in c.GetThingList(__instance.Map))
            {
                CompTossable tossable = thing.TryGetComp<CompTossable>();

                if (tossable != null)
                {
                    tossable.CalculateTossFromExplosion(__instance, thing);
                }
            }
        }
    }
    */
    /*
    [HarmonyPatch(typeof(DamageWorker))]
    public static class DamageWorker_patches
    {
        [HarmonyPatch(typeof(DamageWorker), "Apply")]
        [HarmonyPrefix]
        public static void Apply_prefix(ref DamageInfo dinfo, Thing victim, ref DamageWorker.DamageResult __result)
        {
            CompTossable tossable = victim.TryGetComp<CompTossable>();

            if (tossable == null)
            {
                return true;
            }

            //check if damage would kill
            float num = dinfo.Amount;
            if (victim.def.category == ThingCategory.Plant)
            {
                num *= dinfo.Def.plantDamageFactor;
            }
            else if (victim.def.IsCorpse)
            {
                num *= dinfo.Def.corpseDamageFactor;
            }


            return true;
        }
    }
    */
     
    [HarmonyPatch(typeof(Thing))]
    public static class Thing_patches
    {

        [HarmonyPatch(typeof(Thing), "Destroy")]
        [HarmonyPrefix]
        public static bool Destroy_patch(DestroyMode mode, Thing __instance)
        {
            if (__instance is Pawn) { return true; }

            //TKS_Ragdoll.DebugMessage(__instance.ThingID + " running destroy prefix");
            if (__instance.TryGetComp<CompTossable>() != null && __instance.TryGetComp<CompTossable>().tossing)
            {
                __instance.TryGetComp<CompTossable>().StopToss();
            }

            return true;
        }

        [HarmonyPatch(typeof(Thing), "DeSpawn")]
        [HarmonyPrefix]
        public static bool DeSpawn_prefix(DestroyMode mode, Thing __instance)
        {
            //TKS_Ragdoll.DebugMessage(__instance.ThingID + " running despawn prefix");
            if (__instance is Pawn) { return true; }

            if (__instance.TryGetComp<CompTossable>() != null && __instance.TryGetComp<CompTossable>().tossing)
            {
                __instance.TryGetComp<CompTossable>().StopToss();
            }

            return true;
        }

        [HarmonyPatch(typeof(Thing), "Draw")]
        [HarmonyPrefix]
        public static bool Draw_patch(Thing __instance)
        {
            if (__instance is Pawn)
            {
                return true;
            }

            if (__instance.TryGetComp<CompTossable>() != null && __instance.TryGetComp<CompTossable>().tossing)
            {
                //TKS_Ragdoll.DebugMessage("overriding draw funciton for "+__instance.ThingID+" due to toss");

                __instance.DrawAt(__instance.TryGetComp<CompTossable>().tweenPoint);
                return false;                
            }

            return true;
        }

        [HarmonyPatch(typeof(Thing), "Kill")]
        [HarmonyPrefix]
        public static bool Kill_prefix(DamageInfo dinfo, Hediff exactCulprit, Thing __instance)
        {
            //TKS_Ragdoll.DebugMessage(__instance.ThingID + " running kill prefix");
            if (__instance.TryGetComp<CompTossable>() != null && __instance.TryGetComp<CompTossable>().tossing)
            {
                //dont let tossing things die (add option or something)
                //__instance.HitPoints = 1;
                //return false;
                __instance.TryGetComp<CompTossable>().StopToss();
            }

            return true;
        }

        [HarmonyPatch(typeof(Thing), "Notify_Explosion")]
        [HarmonyPrefix]
        public static bool Notify_Explosion_patch(Explosion explosion, ref Thing  __instance)
        {
            //TKS_Ragdoll.DebugMessage("explosion prefix begun on " + __instance.ThingID);

            FieldInfo explosionCellsField = typeof(Explosion).GetField("cellsToAffect", BindingFlags.NonPublic | BindingFlags.Instance);

            List<IntVec3> explosionCells = (List<IntVec3>)explosionCellsField.GetValue(explosion);

            if (!explosionCells.Contains(__instance.Position))
            {
                return true;
            }

            CompTossable tossable = __instance.TryGetComp<CompTossable>();

            if (tossable == null)
            {
                return true;
            }

            tossable.CalculateTossFromExplosion(explosion, __instance);

            if (__instance is Pawn)
            {
                Pawn pawn = (Pawn)__instance;
                pawn.mindState.Notify_Explosion(explosion);
            }

            return false;

        }
    }

}
