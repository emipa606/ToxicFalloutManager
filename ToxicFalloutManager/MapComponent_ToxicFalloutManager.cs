using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace ToxicFalloutManager
{
    public class MapComponent_ToxicFalloutManager : MapComponent
    {

        public const string TOXIC_NAME_H = "ToxicH";
        public const string TOXIC_NAME_A = "ToxicA";

        public List<Area> exemptedAreas;
        public const string PSYCHE_NAME = "Psyche";

        public bool enabled;

        public bool toxicFallout;
        public bool toxicLatch;

        public Dictionary<Pawn, bool> toxicBounces;

        public Dictionary<Pawn, Area> lastPawnAreas;

        public int slowDown;

        public Area humanToxic;
        public Area animalToxic;

        public MapComponent_ToxicFalloutManager(Map map) : base(map)
        {
            this.enabled = true;
            this.toxicFallout = false;
            this.toxicLatch = false;
            this.toxicBounces = new Dictionary<Pawn, bool>();
            this.lastPawnAreas = new Dictionary<Pawn, Area>();
            this.exemptedAreas = new List<Area>();
            this.slowDown = 0;
            LongEventHandler.QueueLongEvent(ensureComponentExists, null, false, null);
        }

        public static void ensureComponentExists()
        {
            foreach (Map m in Find.Maps)
            {
                if (m.GetComponent<MapComponent_ToxicFalloutManager>() == null)
                {
                    m.components.Add(new MapComponent_ToxicFalloutManager(m));
                }
            }
        }

        public void initPlayerAreas()
        {
            this.humanToxic = null;
            this.animalToxic = null;
            foreach (Area a in map.areaManager.AllAreas)
            {
                if (a.ToString() == TOXIC_NAME_H)
                {
                    if (a.AssignableAsAllowed(AllowedAreaMode.Humanlike))
                    {
                        this.humanToxic = a;
                    }
                    else
                    {
                        a.SetLabel(TOXIC_NAME_H + "2");
                    }
                }
                else if (a.ToString() == TOXIC_NAME_A)
                {
                    if (a.AssignableAsAllowed(AllowedAreaMode.Animal))
                    {
                        this.animalToxic = a;
                    }
                    else
                    {
                        a.SetLabel(TOXIC_NAME_A + "2");
                    }
                }
                else if(a.Label == PSYCHE_NAME)
                {
                    if (!exemptedAreas.Contains(a))
                    {
                        exemptedAreas.Add(a);
                    }
                }
            }
            if (this.humanToxic == null)
            {
                Area_Allowed newHumanToxic;
                map.areaManager.TryMakeNewAllowed(AllowedAreaMode.Humanlike, out newHumanToxic);
                newHumanToxic.SetLabel(TOXIC_NAME_H);
                this.humanToxic = newHumanToxic;
            }
            if (this.animalToxic == null)
            {
                Area_Allowed newAnimalToxic;
                map.areaManager.TryMakeNewAllowed(AllowedAreaMode.Animal, out newAnimalToxic);
                newAnimalToxic.SetLabel(TOXIC_NAME_A);
                this.animalToxic = newAnimalToxic;
            }
        }

        public bool isToxicFallout()
        {
            return map.mapConditionManager.ConditionIsActive(MapConditionDefOf.ToxicFallout);
        }

        public bool isPawnAnimal(Pawn p)
        {
            if (p.needs.joy == null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void considerPawn(Pawn p)
        {
            foreach (Hediff h in p.health.hediffSet.hediffs)
            {
                if (h.def.Equals(HediffDefOf.ToxicBuildup))
                {
                    if (h.Severity < 0.25F)
                    {
                        this.toxicBounces[p] = false;
                        p.playerSettings.AreaRestriction = this.lastPawnAreas[p];
                        return;
                    }
                    else if (h.Severity > 0.35F || this.toxicBounces[p])
                    {
                        this.toxicBounces[p] = true;
                        if (isPawnAnimal(p))
                        {
                            p.playerSettings.AreaRestriction = this.animalToxic;
                            return;
                        }
                        else
                        {
                            p.playerSettings.AreaRestriction = this.humanToxic;
                            return;
                        }
                    }
                }
            }
            this.toxicBounces[p] = false;
            p.playerSettings.AreaRestriction = this.lastPawnAreas[p];
            return;
        }

        public override void MapComponentTick()
        {
            if (enabled)
            {
                slowDown++;
                if (slowDown < 100)
                {
                    return;
                }
                else
                {
                    slowDown = 0;
                }

                initPlayerAreas();

                this.toxicFallout = isToxicFallout();
                if (this.toxicFallout)
                {
                    this.toxicLatch = true;
                }

                foreach (Pawn p in map.mapPawns.PawnsInFaction(Faction.OfPlayer))
                {
                    if (!toxicBounces.ContainsKey(p))
                    {
                        toxicBounces.Add(p, false);
                    }
                    if (!lastPawnAreas.ContainsKey(p))
                    {
                        lastPawnAreas.Add(p, null);
                    }
                    Area curPawnArea = p.playerSettings.AreaRestriction;
                    if ( curPawnArea == null || ( curPawnArea != this.humanToxic && curPawnArea != this.animalToxic && !exemptedAreas.Contains(curPawnArea) ) )
                    {
                        lastPawnAreas[p] = curPawnArea;
                    }

                    if (!exemptedAreas.Contains(curPawnArea))
                    {
                        if (toxicLatch)
                        {
                            if (toxicFallout)
                            {
                                considerPawn(p);
                            }
                            else
                            {
                                p.playerSettings.AreaRestriction = this.lastPawnAreas[p];
                                this.toxicLatch = false;
                            }
                        }
                    }

                }
            }
        }

    }
}
