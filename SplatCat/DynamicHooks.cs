using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoMod.RuntimeDetour;
using UnityEngine;
using System.Reflection;

namespace SplatCat
{
    static class DynamicHooks
    {
        public static void SearchAndAdd()
        {
            // Find all classes that inherit from PlayerGraphics
            Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();

            foreach (Assembly asm in asms)
            {
                try
                {
                    if (asm.GetName().Name == "MonoMod.Utils") continue;
                    Type[] types = asm.GetTypes();
                    for (int i = 0; i < types.Length; i++)
                    {
                        Type type = types[i];
                        if (typeof(PlayerGraphics).IsAssignableFrom(type))
                        {
                            // For each, generate a hook on AddToContainer and DrawSprites
                            GenerateHooks(type);
                        }
                    }
                } catch (Exception e)
                {
                    Debug.LogError("Failed to generate SplatCat hooks for assembly: " + asm.FullName);
                    Debug.LogError(e);
                }
            }

        }

        private static Type[] _AddToContainer_args = new Type[] { typeof(RoomCamera.SpriteLeaser), typeof(RoomCamera), typeof(FContainer) };
        private static Type[] _DrawSprites_args = new Type[] { typeof(RoomCamera.SpriteLeaser), typeof(RoomCamera), typeof(float), typeof(Vector2) };
        private static void GenerateHooks(Type pgType) {
            if (pgType == null) return;
            
            MethodInfo addToContainer = pgType.GetMethod("AddToContainer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, _AddToContainer_args, null);
            if (addToContainer != null)
                new Hook(addToContainer, typeof(SplatCat).GetMethod("PlayerGraphics_AddToContainer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));
            
            MethodInfo drawSprites = pgType.GetMethod("DrawSprites", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, _DrawSprites_args, null);
            if (drawSprites != null)
                new Hook(drawSprites, typeof(SplatCat).GetMethod("PlayerGraphics_DrawSprites", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));
        }
    }
}
