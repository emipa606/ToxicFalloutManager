using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ToxicFalloutManager;

public class MapComponent_ToxicFalloutManager : MapComponent
{
    public const string TOXIC_NAME_H = "ToxicH";
    public const string TOXIC_NAME_A = "ToxicA";
    public readonly string[] EXEMPTED_NAMES = { "Psyche", "Joy", "Medi" };
    public Area animalToxic;

    public bool enabled;

    public List<Area> exemptedAreas;

    public Area humanToxic;

    public Dictionary<Pawn, Area> lastPawnAreas;

    public int slowDown;

    public Dictionary<Pawn, bool> toxicBounces;

    public bool toxicFallout;
    public bool toxicLatch;

    public MapComponent_ToxicFalloutManager(Map map) : base(map)
    {
        enabled = true;
        toxicFallout = false;
        toxicLatch = false;
        toxicBounces = new Dictionary<Pawn, bool>();
        lastPawnAreas = new Dictionary<Pawn, Area>();
        exemptedAreas = new List<Area>();
        slowDown = 0;
        LongEventHandler.QueueLongEvent(ensureComponentExists, null, false, null);
    }

    public static void ensureComponentExists()
    {
        foreach (var m in Find.Maps)
        {
            if (m.GetComponent<MapComponent_ToxicFalloutManager>() == null)
            {
                m.components.Add(new MapComponent_ToxicFalloutManager(m));
            }
        }
    }

    public void initPlayerAreas()
    {
        humanToxic = null;
        animalToxic = null;
        foreach (var a in map.areaManager.AllAreas)
        {
            if (a.ToString() == TOXIC_NAME_H)
            {
                if (a.AssignableAsAllowed())
                {
                    humanToxic = a;
                }
                else
                {
                    a.SetLabel(TOXIC_NAME_H + "2");
                }
            }
            else if (a.ToString() == TOXIC_NAME_A)
            {
                if (a.AssignableAsAllowed())
                {
                    animalToxic = a;
                }
                else
                {
                    a.SetLabel(TOXIC_NAME_A + "2");
                }
            }
            else if (EXEMPTED_NAMES.Contains(a.Label))
            {
                if (!exemptedAreas.Contains(a))
                {
                    exemptedAreas.Add(a);
                }
            }
        }

        if (humanToxic == null)
        {
            map.areaManager.TryMakeNewAllowed(out var newHumanToxic);
            newHumanToxic.SetLabel(TOXIC_NAME_H);
            humanToxic = newHumanToxic;
        }

        if (animalToxic != null)
        {
            return;
        }

        map.areaManager.TryMakeNewAllowed(out var newAnimalToxic);
        newAnimalToxic.SetLabel(TOXIC_NAME_A);
        animalToxic = newAnimalToxic;
    }

    public bool isToxicFallout()
    {
        foreach (var gc in map.gameConditionManager.ActiveConditions)
        {
            if (gc.def == GameConditionDefOf.ToxicFallout)
            {
                return true;
            }
        }

        return false;
    }

    public bool isPawnAnimal(Pawn p)
    {
        if (p.needs.joy == null)
        {
            return true;
        }

        return false;
    }

    public void considerPawn(Pawn p)
    {
        foreach (var h in p.health.hediffSet.hediffs)
        {
            if (!h.def.Equals(HediffDefOf.ToxicBuildup))
            {
                continue;
            }

            if (h.Severity < 0.25F)
            {
                toxicBounces[p] = false;
                p.playerSettings.AreaRestriction = lastPawnAreas[p];
                return;
            }

            if (!(h.Severity > 0.35F) && !toxicBounces[p])
            {
                continue;
            }

            toxicBounces[p] = true;
            if (isPawnAnimal(p))
            {
                p.playerSettings.AreaRestriction = animalToxic;
                return;
            }

            p.playerSettings.AreaRestriction = humanToxic;
            return;
        }

        toxicBounces[p] = false;
        p.playerSettings.AreaRestriction = lastPawnAreas[p];
    }

    public override void MapComponentTick()
    {
        if (!enabled)
        {
            return;
        }

        slowDown++;
        if (slowDown < 100)
        {
            return;
        }

        slowDown = 0;

        initPlayerAreas();

        toxicFallout = isToxicFallout();
        if (toxicFallout)
        {
            toxicLatch = true;
        }

        foreach (var p in map.mapPawns.PawnsInFaction(Faction.OfPlayer))
        {
            if (!toxicBounces.ContainsKey(p))
            {
                toxicBounces.Add(p, false);
            }

            if (!lastPawnAreas.ContainsKey(p))
            {
                lastPawnAreas.Add(p, null);
            }

            var curPawnArea = p.playerSettings.AreaRestriction;
            if (curPawnArea == null || curPawnArea != humanToxic && curPawnArea != animalToxic &&
                !exemptedAreas.Contains(curPawnArea))
            {
                lastPawnAreas[p] = curPawnArea;
            }

            if (exemptedAreas.Contains(curPawnArea))
            {
                continue;
            }

            if (!toxicLatch)
            {
                continue;
            }

            if (toxicFallout)
            {
                considerPawn(p);
            }
            else
            {
                p.playerSettings.AreaRestriction = lastPawnAreas[p];
                toxicLatch = false;
            }
        }
    }
}