using System;
using System.Collections.Generic;
using UnityEngine;
using RWCustom;
using BepInEx;
using System.Security.Permissions;

[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace SplatCat
{
    [BepInPlugin("SplatCat", "Splat Cat", "1.2")] // (GUID, mod name, mod version)
    public class SplatCat : BaseUnityPlugin
    {
        internal static List<DeformContainer> deforms = new List<DeformContainer>();

        public static float squishMultiplier = 1f;
        public static float squishDurationMultiplier = 1f;
        public static float squishFrequencyMultiplier = 1f;

        private static float decayTime = 1.4f;

        public void OnEnable()
        {
            On.RainWorld.OnModsInit += OnModInit;

            // Squish system
            On.RoomCamera.SpriteLeaser.CleanSpritesAndRemove += SpriteLeaser_CleanSpritesAndRemove;
            On.RainWorld.Update += RainWorld_Update;

            // Squish events
            On.Player.TerrainImpact += Player_TerrainImpact;
            On.Creature.Violence += Creature_Violence;
        }

        private void OnModInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);
            MachineConnector.SetRegisteredOI("SplatCat", new SplatCatRemixMenu(this));
        }

        private bool _hasAddedHooks = false;
        private void RainWorld_Update(On.RainWorld.orig_Update orig, RainWorld self)
        {
            // Dynamic hooks (applying deforms)
            // Partiality's loadPriority does nothing, so this must be done here
            // LEGACY CODE
            if (!_hasAddedHooks)
            {
                _hasAddedHooks = true;
                DynamicHooks.SearchAndAdd();
            }

            // Remove deforms with dead references
            // Most of the time SpriteLeaser_CleanSpritesAndRemove should catch this
            for (int i = deforms.Count - 1; i >= 0; i--) deforms[i].StartFrame();

            orig(self);
        }

        public static void ModifySquish(ref float magnitude, ref float duration)
        {
            float invMag = 1f / (1f - magnitude) - 1f;
            invMag *= squishMultiplier;
            magnitude = 1f - 1f / (invMag + 1f);

            //duration *= squishDurationMultiplier;
            duration = Mathf.Clamp01(1 - Mathf.Log(magnitude, 0.001f)) * decayTime * squishDurationMultiplier;
        }

        private void Creature_Violence(On.Creature.orig_Violence orig, Creature self, BodyChunk source, Vector2? directionAndMomentum, BodyChunk hitChunk, PhysicalObject.Appendage.Pos hitAppendage, Creature.DamageType type, float damage, float stunBonus)
        {
            if ((self is Player ply) && directionAndMomentum.HasValue && TryGetDeform(ply, out DeformContainer deform))
            {
                float magnitude = Mathf.Clamp01(Mathf.InverseLerp(0f, 2f, damage)) * 0.3f + 0.3f;
                float duration = magnitude * 0.5f + 0.5f;
                deform.Squish(hitChunk.pos + directionAndMomentum.Value.normalized * hitChunk.rad, directionAndMomentum.Value, magnitude, duration);
            }
            orig(self, source, directionAndMomentum, hitChunk, hitAppendage, type, damage, stunBonus);
        }

        private void Player_TerrainImpact(On.Player.orig_TerrainImpact orig, Player self, int chunk, IntVector2 direction, float speed, bool firstContact)
        {
            float magnitude = Mathf.Clamp01(Mathf.InverseLerp(10f, 40f, speed)) * 0.65f;
            if (magnitude > 0f)
            {
                if (TryGetDeform(self, out DeformContainer deform))
                {
                    BodyChunk c = self.bodyChunks[chunk];
                    float duration = Mathf.Clamp01(speed / 40f) * 0.15f + 0.35f;
                    deform.Squish(c.pos + c.vel.normalized * c.rad, direction.ToVector2(), magnitude, duration);
                }
            }
            orig(self, chunk, direction, speed, firstContact);
        }

        public static void PlayerGraphics_DrawSprites(On.PlayerGraphics.orig_DrawSprites orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);
            if (TryGetDeform(self.owner as Player, out DeformContainer deform))
                deform.DrawSprites(self, rCam, timeStacker);
        }

        private void SpriteLeaser_CleanSpritesAndRemove(On.RoomCamera.SpriteLeaser.orig_CleanSpritesAndRemove orig, RoomCamera.SpriteLeaser self)
        {
            if (self.drawableObject is PlayerGraphics pg)
                if (TryGetDeform(pg.owner as Player, out DeformContainer deform))
                    deforms.Remove(deform);
            orig(self);
        }
        
        public static void PlayerGraphics_AddToContainer(On.PlayerGraphics.orig_AddToContainer orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
        {
            if (!TryGetDeform(self.owner as Player, out DeformContainer deform))
            {
                deform = new DeformContainer(self.owner as Player);
                deforms.Add(deform);
            }

            orig(self, sLeaser, rCam, newContatiner);

            if (newContatiner == null) newContatiner = rCam.ReturnFContainer("Midground");

            // Don't add to the container twice if it was already hooked
            bool alreadyUsed = false;
            for (int i = 0; i < sLeaser.sprites.Length; i++)
            {
                FContainer c = sLeaser.sprites[i].container;
                if ((c == deform.foreground[1]) || (c == deform.background[1]))
                {
                    alreadyUsed = true;
                    break;
                }
            }

            if (!alreadyUsed)
            {
                deform.AddToContainer(newContatiner, rCam);

                FContainer foreground = rCam.ReturnFContainer("Foreground");
                for (int i = 0; i < sLeaser.sprites.Length; i++)
                {
                    FSprite s = sLeaser.sprites[i];
                    // Add foreground and midground sprites to the corresponding deform containers
                    if (s.container == newContatiner) deform.AddChild(s, false);
                    if (s.container == foreground) deform.AddChild(s, true);
                    // Leave sprites in other containers alone
                }
            }
        }

        public static bool TryGetDeform(Player ply, out DeformContainer deform)
        {
            for (int i = 0; i < deforms.Count; i++)
                if (deforms[i].Target == ply)
                {
                    deform = deforms[i];
                    return true;
                }
            deform = null;
            return false;
        }

        // Holds a weak reference to a player, along with sprite containers and deform information
        public class DeformContainer
        {
            private WeakReference _target;

            public FContainer[] foreground = new FContainer[2];
            public FContainer[] background = new FContainer[2];

            public PhysicalObject Target => _target.Target as PhysicalObject;

            public float animPerc;
            public Vector2 animPoint;
            public float animAxis;
            public float animStrength;
            public float animDuration;
            public bool updatedThisFrame = false;

            public DeformContainer(Player ply)
            {
                animPerc = 1f;
                foreground[0] = new FContainer();
                foreground[1] = new FContainer();
                foreground[0].AddChild(foreground[1]);
                background[0] = new FContainer();
                background[1] = new FContainer();
                background[0].AddChild(background[1]);
                _target = new WeakReference(ply);
            }

            public void Squish(Vector2 center, Vector2 normal, float magnitude, float duration)
            {
                ModifySquish(ref magnitude, ref duration);

                //if (animStrength * (1f - animPerc) > magnitude) return;
                if (animStrength * Mathf.Pow(0.001f, animPerc * animDuration / (decayTime * squishDurationMultiplier) ) > magnitude) return;
                animPerc = 0f;
                animPoint = center - GetCenter();
                animAxis = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg;
                animStrength = magnitude;
                animDuration = Mathf.Max(duration, 0.00001f);
            }

            public void StartFrame()
            {
                updatedThisFrame = false;
                PhysicalObject t = Target;
                if (t == null || t.slatedForDeletetion)
                {
                    deforms.Remove(this);
                    foreground[0].RemoveFromContainer();
                    background[0].RemoveFromContainer();
                }
            }

            public Vector2 GetCenter()
            {
                Vector2 pos = new Vector2(0f, 0f);

                PhysicalObject t = Target;
                for (int i = 0; i < t.bodyChunks.Length; i++)
                    pos += t.bodyChunks[i].pos;

                return pos / Math.Max(t.bodyChunks.Length, 1);
            }

            public Vector2 GetCenter(float timeStacker)
            {
                Vector2 pos = new Vector2(0f, 0f);

                PhysicalObject t = Target;
                for (int i = 0; i < t.bodyChunks.Length; i++)
                    pos += Vector2.Lerp(t.bodyChunks[i].lastPos, t.bodyChunks[i].pos, timeStacker);

                return pos / Math.Max(t.bodyChunks.Length, 1);
            }

            public void DrawSprites(PlayerGraphics self, RoomCamera cam, float timeStacker)
            {
                Vector2 center = GetCenter(timeStacker) + animPoint;
                center -= cam.pos;

                if (animPerc < 1f)
                {
                    if (!updatedThisFrame)
                    {
                        updatedThisFrame = true;
                        animPerc = Mathf.Clamp01(animPerc + Time.deltaTime / animDuration);
                    }

                    float axis = animAxis;
                    Vector2 scale;
                    float coefficientOfSquish = Mathf.Sin(Mathf.Pow(animPerc * animDuration, 1.3f) * Mathf.PI * 15f * squishFrequencyMultiplier + Mathf.PI * 1.5f) //squish phase -1 ~ 1
                        // * (1 - animPerc) old linear decay
                        * Mathf.Pow(0.001f, animPerc * animDuration / (decayTime * squishDurationMultiplier)) //exponential decay
                        * animStrength;
                    // scale.x = coefficientOfSquish + 1f; old linear squish
                    // scale.y = 1f / scale.x;
                    coefficientOfSquish = (1f + Mathf.Min(coefficientOfSquish, 0)) / (1f - Mathf.Max(coefficientOfSquish, 0));
                    scale.x = coefficientOfSquish; // direction of squish
                    scale.y = 1f / scale.x; //perpendicular to squish

                    for (int i = 0; i < 2; i++)
                    {
                        FContainer c = (i == 0) ? foreground[0] : background[0];
                        FContainer c2 = (i == 0) ? foreground[1] : background[1];

                        Vector2 temp = -center;

                        temp = temp.Rotate(-axis);
                        c2.SetPosition(temp);
                        c2.rotation = axis;

                        c.scaleX = scale.x;
                        c.scaleY = scale.y;
                        c.rotation = -axis;
                        c.SetPosition(center);
                    }
                }
            }

            public void AddChild(FNode sprite, bool isForeground)
            {
                (isForeground ? foreground[1] : background[1]).AddChild(sprite);
            }

            public void AddToContainer(FContainer newContainer, RoomCamera cam)
            {
                foreground[1].RemoveAllChildren();
                background[1].RemoveAllChildren();
                cam.ReturnFContainer("Foreground").AddChild(foreground[0]);
                newContainer.AddChild(background[0]);
            }
        }
    }
}

public static class EXVector2
{
    public static Vector2 Rotate(this Vector2 v, float degrees)
    {
        float sin = Mathf.Sin(degrees * Mathf.Deg2Rad);
        float cos = Mathf.Cos(degrees * Mathf.Deg2Rad);

        float tx = v.x;
        float ty = v.y;
        v.x = (cos * tx) - (sin * ty);
        v.y = (sin * tx) + (cos * ty);
        return v;
    }
}