using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Grammar;

namespace TKS_Ragdoll
{

    [DefOf]
    public static class Defs
    {
        public static RulePackDef Event_Tossed_Explosion;
        public static RulePackDef Event_Tossed_Bullet;
        public static RulePackDef Event_Tossed_Melee;
        public static RulePackDef Event_Tossed_Impact;
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

        public void SliderLabeled(Listing_Standard ls, string label, ref float val, string format, float min = 0f, float max = 1f, string tooltip = null)
        {
            Rect rect = ls.GetRect(Text.LineHeight);
            Rect rect2 = GenUI.Rounded(GenUI.LeftPart(rect, 0.7f));
            Rect rect3 = GenUI.Rounded(GenUI.LeftPart(GenUI.Rounded(GenUI.RightPart(rect, 0.3f)), 0.67f));
            Rect rect4 = GenUI.Rounded(GenUI.RightPart(rect, 0.1f));
            TextAnchor anchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect2, label);
            float num = Widgets.HorizontalSlider(rect3, val, min, max, true, null, null, null, -1f);
            val = num;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(rect4, string.Format(format, val));
            if (!GenText.NullOrEmpty(tooltip))
            {
                TooltipHandler.TipRegion(rect, tooltip);
            }
            Text.Anchor = anchor;
            ls.Gap(ls.verticalSpacing);
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            listingStandard.CheckboxLabeled("TKSDebugPrint".Translate(), ref settings.debugPrint);
            listingStandard.TextFieldNumericLabeled<int>("TKSRagdollMinStun".Translate(), ref settings.minStun, ref editBufferFloat);
            listingStandard.SubLabel("TKSRagdollMinStunDescrip".Translate(), 100.0f);
            this.SliderLabeled(listingStandard, "TKSRagdollAdjust".Translate()+": "+settings.adjust.ToString(), ref settings.adjust, "", 0.0f, 5.0f, "TKSRagdollAdjustToolTip".Translate());
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

        public float adjust = 1.0f;

        public int minStun = 0;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref debugPrint, "debugPrint");
            Scribe_Values.Look(ref adjust, "adjust");
            Scribe_Values.Look(ref minStun, "minStun");

            base.ExposeData();
        }
    }

    public class BattleLogEntry_TossImpact : BattleLogEntry_Event
    {
        public BattleLogEntry_TossImpact(Thing subject, RulePackDef eventDef, Thing initiator, Thing edifice) : base(subject, eventDef, initiator)
        {
            if (subject is Pawn)
            {
                this.subjectPawn = (subject as Pawn);
            }
            else if (subject != null)
            {
                this.subjectThing = subject.def;
            }
            if (initiator is Pawn)
            {
                this.initiatorPawn = (initiator as Pawn);
            }
            else if (initiator != null)
            {
                this.initiatorThing = initiator.def;
            }
            this.eventDef = eventDef;

            this.edificeDef = edifice.def;
        }
        protected override GrammarRequest GenerateGrammarRequest()
        {
            GrammarRequest result = base.GenerateGrammarRequest();

            result.Rules.AddRange(GrammarUtility.RulesForDef("EDIFICE", this.edificeDef));

            return result;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look<ThingDef>(ref this.edificeDef, "edificeDef");
        }

        protected ThingDef edificeDef;
    }

    public class ModExtension_BulletToss : DefModExtension
    {
        public int tossMagnitude = 0;
        public float tossFalloff = 1.0f;
    }

    public class ModExtension_MeleeToss : DefModExtension
    {
        public int tossMagnitude = 0;
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

    /* haven't figured out melee yet
    public class SubEffecter_Knockback : SubEffecter
    {
        public int knockbackAmount;

        public SubEffecter_Knockback(SubEffecterDef subDef, Effecter parent) : base(subDef, parent)
        {
            this.knockbackAmount = 1;
        }

        public override void SubTrigger(TargetInfo A, TargetInfo B, int overrideSpawnTick = -1)
        {
            Thing thing = B.Thing;
            Pawn pawn = thing as Pawn;
            if (pawn != null)
            {
                TKS_Ragdoll.DebugMessage("pawn " + pawn.Name + " recieves knockback in subeffector");
            }
        }
    }
    
    public class DamageWorker_Knockback : DamageWorker_AddGlobal
    {
        public override DamageWorker.DamageResult Apply(DamageInfo dinfo, Thing thing)
        {
            Pawn pawn = thing as Pawn;
            if (pawn != null)
            {
                TKS_Ragdoll.DebugMessage("pawn " + pawn.Name + " recieves knockback in damage worker");
            }
            return new DamageWorker.DamageResult();
        }
    }
    */


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

        public void CalculateTossFromMelee(int magnitude, Thing thing, ThingDef weaponDef, Thing source)
        {
            if (magnitude < 1) { return; }

            this.tosserId = source.thingIDNumber;

            float adjust = LoadedModManager.GetMod<TKS_Ragdoll>().GetSettings<TKS_RagdollSettings>().adjust;

            TKS_Ragdoll.DebugMessage(thing.ThingID + " recieved " + magnitude.ToString() + " magnitude at position " + thing.Position + " from melee attack from " + source.ThingID);

            Vector3 impactVector = thing.Position.ToVector3() - source.Position.ToVector3();

            TKS_Ragdoll.DebugMessage(thing.ThingID + " normalized melee vector: " + impactVector.normalized.ToString());

            float proneReduce = 1.0f;
            if (thing is Pawn)
            {
                Pawn pawn = (Pawn)thing;

                if (pawn.Downed)
                {
                    proneReduce = 0.33f;
                    TKS_Ragdoll.DebugMessage(thing.ThingID + " is downed, reducing toss distance by " + proneReduce.ToString() + "%");
                }
            }

            Vector3 tossVector = impactVector.normalized * (magnitude * proneReduce * adjust);

            TKS_Ragdoll.DebugMessage(thing.ThingID + " toss Vector: " + tossVector.ToString());

            Vector3 tossPoint = tossVector + thing.Position.ToVector3();
            IntVec3 tossPointVec3 = tossPoint.ToIntVec3();

            TKS_Ragdoll.DebugMessage(thing.ThingID + " tossing to " + tossPointVec3.ToString());

            //stop pawns from moving
            if (thing != null && thing is Pawn)
            {
                Pawn pawn = (Pawn)thing;
                if (pawn.pather != null)
                {
                    pawn.pather.StopDead();
                    //__instance.pather.StartPath(tossDest, PathEndMode.OnCell);
                }

                if (source != null)
                {
                    Find.BattleLog.Add(new BattleLogEntry_Event(pawn, Defs.Event_Tossed_Melee, source));
                }
            }

            this.StartToss(thing.Position, tossPointVec3, source);

        }

        public void CalculateTossFromBullet(int magnitude, float falloff, Thing thing, ThingDef weaponDef, Thing source)
        {
            if (magnitude <1) { return; }

            this.tosserId = source.thingIDNumber;

            float adjust = LoadedModManager.GetMod<TKS_Ragdoll>().GetSettings<TKS_RagdollSettings>().adjust;

            TKS_Ragdoll.DebugMessage(thing.ThingID + " recieved " + magnitude.ToString() + " magnitude (falloff "+falloff.ToString()+") at position " + thing.Position + " from bullet from "+source.ThingID);

            Vector3 impactVector = thing.Position.ToVector3() - source.Position.ToVector3();

            TKS_Ragdoll.DebugMessage(thing.ThingID + " normalized bullet vector: " + impactVector.normalized.ToString());

            //determine falloff
            float range = weaponDef.Verbs.First().range;
            float distanceTravelled = impactVector.magnitude;

            //if pawn is prone reduce by 66%
            float proneReduce = 1.0f;
            if (thing is Pawn)
            {
                Pawn pawn = (Pawn)thing;

                if (pawn.Downed)
                {
                    proneReduce = 0.33f;
                    TKS_Ragdoll.DebugMessage(thing.ThingID + " is downed, reducing toss distance by "+proneReduce.ToString()+"%");
                }
            }

            float reduceBy = (Mathf.Abs(impactVector.magnitude) / range) * falloff;

            TKS_Ragdoll.DebugMessage(thing.ThingID + " range of weapon & falloff reduces impact magnutide by "+reduceBy.ToString());

            Vector3 tossVector = impactVector.normalized * (magnitude * proneReduce * adjust * (1.0f - reduceBy));

            TKS_Ragdoll.DebugMessage(thing.ThingID + " toss Vector: " + tossVector.ToString());

            Vector3 tossPoint = tossVector + thing.Position.ToVector3();
            IntVec3 tossPointVec3 = tossPoint.ToIntVec3();

            TKS_Ragdoll.DebugMessage(thing.ThingID + " tossing to " + tossPointVec3.ToString());

            //stop pawns from moving
            if (thing != null && thing is Pawn)
            {
                Pawn pawn = (Pawn)thing;
                if (pawn.pather != null)
                {
                    pawn.pather.StopDead();
                    //__instance.pather.StartPath(tossDest, PathEndMode.OnCell);
                }

                if (source != null)
                {
                    Find.BattleLog.Add(new BattleLogEntry_Event(pawn, Defs.Event_Tossed_Bullet, source));
                }
            }

            this.StartToss(thing.Position, tossPointVec3, source);

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

            this.damage = damage;

            float adjust = LoadedModManager.GetMod<TKS_Ragdoll>().GetSettings<TKS_RagdollSettings>().adjust;

            if (!explosion.damageFalloff)
            {
                //adjust value at cell
                adjust = (explosion.radius - thing.Position.DistanceTo(explosion.Position)) / explosion.radius;
                TKS_Ragdoll.DebugMessage(explosion.ThingID + " no falloff, adjusting toss distance by " + adjust.ToString());

            }

            Vector3 impactVector = new Vector3();

            if (thing.Position.ToVector3() != explosion.Position.ToVector3())
            {
                impactVector = thing.Position.ToVector3() - explosion.Position.ToVector3();
            }
            else
            {
                //toss randomly
                IntVec3 above = new IntVec3(Mathf.RoundToInt(UnityEngine.Random.Range(-1, 1)), 0, Mathf.RoundToInt(UnityEngine.Random.Range(-1, 1)));
                IntVec3 up = thing.Position + above;
                impactVector = thing.Position.ToVector3() - up.ToVector3();
            }

            impactVector = impactVector.normalized;

            TKS_Ragdoll.DebugMessage(thing.ThingID + " normalized explosion vector: " + impactVector.ToString());

            Vector3 tossVector = impactVector * (damage * adjust);

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

                Find.BattleLog.Add(new BattleLogEntry_Event(pawn, Defs.Event_Tossed_Explosion, explosion.instigator));
            }

            this.StartToss(thing.Position, tossPointVec3, explosion.instigator);

        }

        public void StartToss(IntVec3 start, IntVec3 dest, Thing instigator, int mapNumber = 0)
        {


            ThingWithComps thing = this.parent;
            this.instigator = instigator;

            if (thing == null)
            {
                Log.Warning("attempt begin toss with null object");
                return;
            }

            Map map = thing.Map;

            if (map == null)
            {
                //try through ID
                if (mapNumber == 0) { mapNumber = thing.thingIDNumber; };

                map = TKSUtils.GetMap(mapNumber);

                if (map == null)
                {
                    Log.Warning("attempt to begin toss without map");
                    return;
                }
                
            }

            int currentTick = Find.TickManager.TicksGame;

            this.tossing = true;
            this.progress = 1;
            this.tweenProgress = 1;
            this.destination = dest;
            this.hitSomething = null;
            this.start = start;

            //add some jitter to start tick so all objects that got exploded dont tick on the same tick
            this.startTick = currentTick + Mathf.RoundToInt(UnityEngine.Random.Range(-1, 1));
            this.tweenPoint = start.ToVector3();

            DrawLine(this.start, this.destination);

            MapComponent_Toss tossComponent = map.GetComponent<MapComponent_Toss>();

            /* instead of checking for obstructions all at once (which is heavy when many pawns/things are hit with the same explosion)
             * we'll have the pawn/thing check just the next tile before each move
            
            TKS_Ragdoll.DebugMessage(thing.ThingID + " checking toss arc for obstructions");

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
            */

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

            TKS_Ragdoll.DebugMessage(thing.ThingID + " toss begun on tick "+ currentTick.ToString()+" from " + start.ToString() + " to " + dest.ToString()+ ": ["+String.Join(", ", this.line)+"]");

            this.tossVector = -(dest.ToVector3() - start.ToVector3()).normalized;
            this.tossAngle = Vector3.Angle(start.ToVector3(), dest.ToVector3());

            if (!(thing is Pawn))
            {
                map.Tossers().TossThis(thing);
            }
            else
            {
                //try to stop them from what they're doing
                Pawn pawn = (Pawn)thing;

                if (pawn?.jobs?.curDriver != null && !pawn.Drafted)
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

            int currentTick = Find.TickManager.TicksGame;

            int tossRate = 4;

            IntVec3 currentPoint = thing.Position;

            if ((currentTick - this.startTick) % tossRate == 0)
            {
                TKS_Ragdoll.DebugMessage(thing.ThingID + " is being tossed (tick " + currentTick.ToString() + ", startTick: " + this.startTick.ToString() + ")");

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

                    //check for impact
                    Building edifice = newPoint.GetEdifice(tossComponent.map);

                    if (edifice != null)
                    {
                        this.hitSomething = edifice;
                    }

                    StopToss();
                    return;
                }

                TKS_Ragdoll.DebugMessage(thing.ThingID + " setting to " + newPoint.ToString());

                thing.Position = newPoint;

                this.progress += 1;

                this.tweenProgress = 1;

                if (newPoint == this.destination || (newPoint.x == this.destination.x && newPoint.z == this.destination.z))
                {
                    TKS_Ragdoll.DebugMessage(thing.ThingID + " toss complete");
                    thing.Position = new IntVec3(thing.Position.x, 0, thing.Position.z);
                    StopToss();
                    return;
                }

            }
            else
            {
                //tween non-pawn objects
                if (!(thing is Pawn))
                {
                    float percentage = (float)this.tweenProgress / (float)tossRate;
                    //TKS_Ragdoll.DebugMessage(thing.ThingID + " tween is "+percentage.ToString()+"% done");
                    Vector3 tweenPoint = percentage * this.tossVector;
                    TKS_Ragdoll.DebugMessage(thing.ThingID + " tween point " + tweenPoint);
                    tweenPoint = currentPoint.ToVector3() - tweenPoint;
                    TKS_Ragdoll.DebugMessage(thing.ThingID + " tweening to " + tweenPoint);

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

        public void StopToss()
        {
            this.tossing = false;
            this.tosserId = 0;

            ThingWithComps thing = this.parent;

            if (thing == null || thing.Destroyed) { return; }

            int stunTicks = LoadedModManager.GetMod<TKS_Ragdoll>().GetSettings<TKS_RagdollSettings>().minStun;

            //do impact if any
            if (this.hitSomething != null)
            {
                TKS_Ragdoll.DebugMessage(thing.ThingID+" hits "+this.hitSomething.ThingID);

                stunTicks += 125;
            }

            //just pawns for now
            if (!(thing is Pawn))
            {
                return; 
            }

            Pawn pawn = thing as Pawn;

            if (this.hitSomething!= null)
            {
                Pawn_DrawTracker drawer = pawn.Drawer;

                JitterHandler jitterer = Traverse.Create(drawer).Field("jitterer").GetValue<JitterHandler>();

                jitterer.AddOffset(0.17f, this.tossAngle);

                //get the magnitude of the impact
                float magnitude = 1.0f - ((float)this.progress / (float)this.line.Count);

                int impactDamage = (int)(magnitude * damage);

                TKS_Ragdoll.DebugMessage(thing.ThingID + " recieves " + impactDamage.ToString() + " damage from impact (magnitude " + magnitude.ToString() + ")");

                if (impactDamage != 0)
                {
                    List<BodyPartDef> possibleImpacts = new List<BodyPartDef>() { BodyPartDefOf.Arm, BodyPartDefOf.Leg, BodyPartDefOf.Head, BodyPartDefOf.Torso, BodyPartDefOf.Body };

                    possibleImpacts.Shuffle();

                    var parts = pawn.health.hediffSet.GetNotMissingParts();

                    BodyPartRecord partToHit = null;

                    foreach (BodyPartDef partDef in possibleImpacts)
                    {
                        partToHit = parts.Where(x => x.def == partDef).FirstOrDefault();
                        if (partToHit == null) { continue; };
                    }

                    if (partToHit != null)
                    {
                        float armorPenetration = 0.0f;

                        TKS_Ragdoll.DebugMessage(thing.ThingID + " recieves damage at part " + partToHit.def.defName);
                        DamageInfo dinfo = new DamageInfo(DamageDefOf.Blunt, impactDamage, armorPenetration, this.tossAngle, null, partToHit, null, DamageInfo.SourceCategory.ThingOrUnknown, null, false, true);

                        LogEntry entry = null;
                        if (this.instigator != null)
                        {
                            entry = new BattleLogEntry_TossImpact(pawn, Defs.Event_Tossed_Impact, instigator, this.hitSomething);
                            Find.BattleLog.Add(entry);
                        }
                        var result = pawn.TakeDamage(dinfo);

                        if (entry != null)
                        {
                            result.AssociateWithLog((LogEntry_DamageResult)entry);
                        }
                        

                    }
                }

            }

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

        public bool tossing = false;

        public IntVec3 destination;

        public Vector3 tweenPoint;

        private int tosserId = 0;

        private Vector3 tossVector;

        private float tossAngle;

        private float damage;

        private IntVec3 start;

        private int progress;

        private int tweenProgress;

        private int startTick;

        private List<IntVec3> line;

        private Thing hitSomething = null;

        private Thing instigator = null;


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
                TKS_Ragdoll.DebugMessage(__instance.Name+" ("+__instance.ThingID+") died during toss at "+__instance.Position+", attempting to toss corpse to toss destination "+tossable.destination.ToString());
                CompTossable tossableCorpse = __result.TryGetComp<CompTossable>();

                if (tossableCorpse != null)
                {
                    IntVec3 startPoint = __instance.Position;
                    IntVec3 destination = tossable.destination;
                    tossableCorpse.StartToss(startPoint, destination, null, __instance.thingIDNumber);

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
                //TKS_Ragdoll.DebugMessage("created corpse for "+def.defName+" with comps "+ String.Join(", ", def.comps));
                yield return def;
            }
        }
    }

    
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
    }
    
    [HarmonyPatch(typeof(Verb_MeleeAttackDamage))]
    public static class Verb_MeleeAttackDamage_patches
    {
        [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget")]
        [HarmonyPostfix]
        public static void ApplyMeleeDamage_Postfix(LocalTargetInfo target, Verb_MeleeAttackDamage __instance)
        {
            TKS_Ragdoll.DebugMessage("ApplyMeleeDamage postfix firing");

            if (__instance.EquipmentSource == null)
            {
                return;
            }

            Thing hitThing = (Thing)target;

            if (hitThing == null) { return; };

            TKS_Ragdoll.DebugMessage("melee hit on " + hitThing.ThingID);

            CompTossable tossable = hitThing.TryGetComp<CompTossable>();

            if (tossable == null) { return; }

            ThingWithComps equipment = __instance.EquipmentSource;
            ThingDef weaponDef = equipment.def;
            Pawn casterPawn = __instance.CasterPawn;

            TKS_Ragdoll.DebugMessage("pawn " + casterPawn.Name + " hits tossable " + hitThing.ThingID + " with " + equipment.def.defName);

            ModExtension_MeleeToss tosser = weaponDef.GetModExtension<ModExtension_MeleeToss>();

            if (tosser == null || tosser.tossMagnitude==0) { return; }

            //float impactAngle = Quaternion.FromToRotation(casterPawn.Position.ToVector3(), hitThing.Position.ToVector3()).eulerAngles[1]);

            tossable.CalculateTossFromMelee(tosser.tossMagnitude, hitThing, weaponDef, equipment);

        }
    }
    
    
    [HarmonyPatch(typeof(Bullet))]
    public static class Bullet_Patches
    {
        [HarmonyPatch(typeof(Bullet), "Impact")]
        [HarmonyPostfix]
        public static void Impact(Thing hitThing, bool blockedByShield, Bullet __instance)
        {
            if (hitThing == null) { return;  };

            CompTossable tossable = hitThing.TryGetComp<CompTossable>();

            if (tossable == null) { return; }

            ModExtension_BulletToss tossProperties = __instance.def.GetModExtension<ModExtension_BulletToss>();

            if (tossProperties == null) { return; }

            int tossDistance = tossProperties.tossMagnitude;
            float tossFalloff = tossProperties.tossFalloff;

            TKS_Ragdoll.DebugMessage(hitThing.ThingID + " hit with bullet, toss distance " + tossDistance.ToString()+", toss falloff "+tossFalloff.ToString());

            //float impactAngle = __instance.ExactRotation.eulerAngles.y;

            Thing launcher = __instance.Launcher;
            ThingDef weaponDef = __instance.EquipmentDef;

            tossable.CalculateTossFromBullet(tossDistance, tossFalloff, hitThing, weaponDef, launcher);

        }
    }

}
