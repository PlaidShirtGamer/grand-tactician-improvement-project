using System.Collections.Generic;
using UnityEngine;

namespace GTIP
{
    public static class HierarchyFormation
    {
        public static void Formation(BattleUnits engine, Regiment root, int formation, float rotation,
            Vector3 target, bool immediate, bool newPath, bool modify, int state, int refuse,
            bool ignoreDeployment, bool skipRotation, bool showOptions, bool entrenchments, bool terrain)
        {
            if (!SixLevelRuntime.Active || root == null || root.unittyp != HierarchyModel.Army)
            {
                NativeAccess.Call(engine, "GTIP_Original_Formation", root, formation, rotation, target, immediate,
                    newPath, modify, state, refuse, ignoreDeployment, skipRotation, showOptions, entrenchments, terrain);
                return;
            }
            var detached = immediate ? BattleUnits.DeParentDetachedSubordinates(root) : null;
            try
            {
                if (rotation == -1) rotation = root.lastsetwaypointrotation;
                if (formation == -1) formation = root.groupformation;
                root.groupformation = formation;
                int leafFormation = formation == 3 ? 2 : formation == 4 ? 3 : 0;
                int brigadeFormation = formation == 1 ? 1 : 0;
                int upperFormation = formation == 2 || formation == 3 ? 2 : 0;
                int level = engine.IsCampaign ? -1 : 13;
                if ((!modify || newPath) && (!GameVars.useorderdelays || immediate || target == default(Vector3)))
                {
                    engine.PerformActionForGroupForAllUnitsOnLevel(root, level, "ClearTargets", null);
                    engine.PerformActionForGroupForAllUnitsOnLevel(root, level, "ChangeRegimentFormation",
                        GameVars.SetFormationParam(leafFormation, true, -1, -1f, true));
                    engine.PerformActionForGroupForAllUnitsOnLevel(root, level, "ResetMarchColumnVariables", null);
                }
                if (!root.withinrotationprocess && modify)
                    engine.PerformActionForGroupForAllUnitsOnLevel(root, level,
                        skipRotation ? "RotateRegimentWithoutStop" : "RotateRegiment", rotation);
                engine.PerformActionForGroupForAllUnitsOnLevel(root, level, "SetGroupSpeed", engine.GetSlowestSpeedOfGroup(root));

                var seen = new HashSet<Regiment>();
                void Measure(Regiment unit)
                {
                    if (!seen.Add(unit)) return;
                    var tree = engine.GetHierarchyTree(unit, true, true, !root.pathinterrupted, false);
                    foreach (var child in tree.attachedunitreg) if (child != null) Measure(child);
                    unit.groupposition = (BattleUnits.GroupPosition)NativeAccess.Call(engine, "BuildGroupOffsetPositions",
                        tree, rotation, unit.unittyp == HierarchyModel.Brigade ? brigadeFormation : upperFormation, formation);
                }
                Measure(root);
                var battlefield = NativeAccess.Get<BattlefieldSetup>(engine, "bfs");
                Vector3 Offset(Vector3 origin, float x, float z) => battlefield.GetAngleTerrainPosXY(origin, rotation, x, z, Terrain.activeTerrain);
                var rootPosition = target;
                if (target == default(Vector3))
                {
                    rootPosition = newPath ? root.transform.position : root.GetLastTransmittedPathPos(true);
                    target = Offset(rootPosition, 0, -root.groupposition.groupdepth - GamePrefs.groupunitdistancetoback);
                }
                if (!engine.IsCampaign) rootPosition = Offset(target, 0, root.groupposition.groupdepth + GamePrefs.groupunitdistancetoback);
                var shape = terrain ? new BattlefieldSetup.TerrainShape(target, rotation, root.groupposition.groupwidth,
                    new List<int> { 4, 5, 6, 9, 10, 11, 12, 13 }) : null;
                void Move(Regiment unit, Vector3 position, float facing)
                {
                    if (unit.permanentlydetached || unit.isrouted) return;
                    if (immediate) unit.SetNewPosition(position, facing, true, ignoreDeployment);
                    else
                    {
                        unit.immediateunitplacement = false;
                        unit.RegimentSetPath(position, newPath, false, facing, modify, state, -1f, root, true, showOptions);
                    }
                }
                root.formationordered = formation;
                Move(root, rootPosition, rotation);
                root.lastsetwaypointposition = target;
                root.lastsetwaypointrotation = rotation;
                seen.Clear();
                void Place(Regiment parent, Vector3 origin)
                {
                    if (!seen.Add(parent)) return;
                    var layout = parent.groupposition;
                    for (int i = 0; i < layout.attachedunits; i++)
                    {
                        var child = layout.attachedunitreg[i];
                        if (child == null) continue;
                        bool headquarters = child.unittyp > 13;
                        float width = headquarters ? child.groupposition.groupwidth : child.GetWidth();
                        float depth = headquarters ? child.groupposition.groupdepth : child.GetDepth();
                        var offset = layout.unitoffsetposition[i];
                        var childOrigin = Offset(origin, -offset.x, offset.z);
                        var position = Offset(origin, -offset.x, offset.z + depth * (headquarters ? 1 : .5f));
                        float facing = rotation;
                        if (shape != null)
                        {
                            var adjusted = shape.InterpolatePosition(position);
                            position = adjusted.position; facing = adjusted.facing;
                        }
                        if (refuse >= 0)
                        {
                            var skewType = typeof(BattleUnits).GetNestedType("SkewedPosition", System.Reflection.BindingFlags.NonPublic);
                            var skew = System.Activator.CreateInstance(skewType, new object[] { position, rotation, offset.x, width,
                                layout.groupwidth, layout.groupwidth * GamePrefs.refuseflankdepth, refuse, GamePrefs.refuseflankskew });
                            position = NativeAccess.Get<Vector3>(skew, "position"); facing = NativeAccess.Get<float>(skew, "rotation");
                        }
                        if (headquarters && !child.permanentlydetached && !child.isrouted)
                        {
                            child.groupformation = child.unittyp == HierarchyModel.Brigade ? brigadeFormation : formation;
                            child.formationordered = formation;
                        }
                        Move(child, position, facing);
                        if (headquarters) Place(child, childOrigin);
                    }
                }
                Place(root, target);
            }
            finally { BattleUnits.ParentDetachedSubordinates(root, detached); }
        }
    }
}
