#if CLIENT
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Barotrauma;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BadAir;

public static class SmokeRenderer
{
    private class SmokeParticle
    {
        public Vector2 LocalPosition; 
        public Vector2 Velocity;
        public float Size;
        public float Rotation;
        public float RotationSpeed;
        public float Alpha;
        public float MaxAlpha;
        public float Lifetime;
        public float MaxLifetime;
    }

    private static readonly Dictionary<Hull, List<SmokeParticle>> hullParticles = new Dictionary<Hull, List<SmokeParticle>>();
    private static readonly Stack<SmokeParticle> particlePool = new Stack<SmokeParticle>();
    
    private static Texture2D? fogTexture;
    private static Sprite fogSpriteRef;
    private static bool fogLoadFailed;
    
    private static readonly Color SmokeColor = new Color(75, 77, 85); 
    private static bool debugLogged = false;

    private static readonly RasterizerState ScissorRasterizer = new RasterizerState
    {
        ScissorTestEnable = true,
        CullMode = (CullMode)0
    };

    private static SmokeParticle GetParticle()
    {
        if (particlePool.Count > 0) return particlePool.Pop();
        return new SmokeParticle();
    }

    private static void UpdateParticles(float deltaTime)
    {
        if (GameMain.GameSession == null || Level.Loaded == null)
        {
            foreach (var list in hullParticles.Values)
            {
                foreach (var p in list) particlePool.Push(p);
            }
            hullParticles.Clear();
            return;
        }

        List<Hull> hullList = Hull.HullList;
        for (int i = 0; i < hullList.Count; i++)
        {
            Hull hull = hullList[i];
            if (hull == null || ((Entity)hull).Removed) continue;

            if (HullAtmosphere.TryGet(hull, out HullAtmosphere atmosphere) && atmosphere.Smoke > 2f)
            {
                // Damage maxes out at smoke = 45, so cap visuals at 50 instead of 100
                float smokePct = MathHelper.Clamp(atmosphere.Smoke / 50f, 0f, 1f); 
                float visualIntensity = smokePct; // Linear scaling so low smoke is clearly visible
                float targetParticleCount = Math.Min(75f, (hull.Volume / 1000f) * visualIntensity * 20f);
                
                if (!hullParticles.TryGetValue(hull, out var particles))
                {
                    particles = new List<SmokeParticle>();
                    hullParticles[hull] = particles;
                }

                if (particles.Count < targetParticleCount)
                {
                    Gap flowGap = null;
                    float highestOtherSmoke = atmosphere.Smoke; // Must be higher than OUR smoke
                    
                    foreach (Gap gap in hull.ConnectedGaps)
                    {
                        if (gap.IsRoomToRoom && gap.Open > 0.1f && ((MapEntity)gap).linkedTo.Count >= 2)
                        {
                            Hull otherHull = (((MapEntity)gap).linkedTo[0] == hull) ? (((MapEntity)gap).linkedTo[1] as Hull) : (((MapEntity)gap).linkedTo[0] as Hull);
                            if (otherHull != null && HullAtmosphere.TryGet(otherHull, out HullAtmosphere otherAtmos))
                            {
                                if (otherAtmos.Smoke > highestOtherSmoke)
                                {
                                    highestOtherSmoke = otherAtmos.Smoke;
                                    flowGap = gap;
                                }
                            }
                        }
                    }

                    float deficit = targetParticleCount - particles.Count;
                    float spawnRateF = deficit * deltaTime * 15f; 
                    int spawnCount = (int)spawnRateF;
                    if (Rand.Range(0f, 1f) < (spawnRateF - spawnCount)) spawnCount++;
                    
                    if (deficit > 5f && spawnCount < 1) spawnCount = 1;
                    if (deficit > 10f) spawnCount = (int)(deficit * 0.5f); // Faster catch-up

                    for (int s = 0; s < spawnCount; s++)
                    {
                        SpawnParticle(hull, particles, smokePct, flowGap);
                    }
                }
            }
        }

        var hullsToRemove = new List<Hull>();
        foreach (var kvp in hullParticles)
        {
            Hull hull = kvp.Key;
            var particles = kvp.Value;
            
            if (((Entity)hull).Removed)
            {
                foreach (var p in particles) particlePool.Push(p);
                hullsToRemove.Add(hull);
                continue;
            }

            Rectangle hullRect = ((MapEntity)hull).Rect;
            float halfWidth = hullRect.Width * 0.5f;
            float halfHeight = hullRect.Height * 0.5f;

            float currentSmoke = 0f;
            if (HullAtmosphere.TryGet(hull, out HullAtmosphere currentAtmosphere))
            {
                currentSmoke = currentAtmosphere.Smoke;
            }

            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.Lifetime += deltaTime;
                
                // If the room has been cleared of smoke, instantly remove particles
                if (currentSmoke < 0.1f)
                {
                    p.Lifetime = p.MaxLifetime;
                }
                else if (currentSmoke < 1f)
                {
                    p.Lifetime += deltaTime * 10f;
                }

                if (p.Lifetime >= p.MaxLifetime)
                {
                    particlePool.Push(p);
                    particles.RemoveAt(i);
                    continue;
                }

                p.Velocity.Y += 5f * deltaTime;
                p.Velocity.X += Rand.Range(-2f, 2f) * deltaTime;
                
                p.LocalPosition += p.Velocity * deltaTime;
                p.Rotation += p.RotationSpeed * deltaTime;
                
                float halfSize = p.Size * 0.1f;
                
                if (p.LocalPosition.X - halfSize < -halfWidth) { p.LocalPosition.X = -halfWidth + halfSize; p.Velocity.X *= -0.5f; }
                if (p.LocalPosition.X + halfSize > halfWidth) { p.LocalPosition.X = halfWidth - halfSize; p.Velocity.X *= -0.5f; }
                if (p.LocalPosition.Y + halfSize > halfHeight) { p.LocalPosition.Y = halfHeight - halfSize; p.Velocity.Y *= -0.5f; }
                if (p.LocalPosition.Y - halfSize < -halfHeight) { p.LocalPosition.Y = -halfHeight + halfSize; p.Velocity.Y *= -0.5f; }

                float lifePct = p.Lifetime / p.MaxLifetime;
                if (lifePct < 0.01f) p.Alpha = MathHelper.Lerp(0f, p.MaxAlpha, lifePct / 0.01f); // Instant 0.1s pop
                else if (lifePct > 0.8f) p.Alpha = MathHelper.Lerp(p.MaxAlpha, 0f, (lifePct - 0.8f) / 0.2f);
                else p.Alpha = p.MaxAlpha;
                
                p.Size += 2f * deltaTime;
            }
        }

        foreach(var h in hullsToRemove)
        {
            hullParticles.Remove(h);
        }
    }

    private static void SpawnParticle(Hull hull, List<SmokeParticle> targetList, float smokePct, Gap flowGap = null)
    {
        Rectangle rect = ((MapEntity)hull).Rect;
        float halfWidth = rect.Width * 0.5f;
        float halfHeight = rect.Height * 0.5f;
        
        Vector2 localPos;
        Vector2 velocity;

        if (flowGap != null && Rand.Range(0f, 1f) < 0.40f) // 40% of particles flow from the door
        {
            Vector2 gapWorldPos = ((Entity)flowGap).WorldPosition;
            Vector2 hullWorldPos = ((Entity)hull).WorldPosition;
            
            if (flowGap.IsHorizontal) // Left/Right door
            {
                gapWorldPos.Y += Rand.Range(-flowGap.Rect.Height * 0.4f, flowGap.Rect.Height * 0.4f);
                localPos = gapWorldPos - hullWorldPos;
                
                if (localPos.X < 0) {
                    localPos.X += 40f; // Shift spawn inside the room to avoid hard scissor cuts
                    velocity = new Vector2(Rand.Range(40f, 120f), Rand.Range(-2f, 15f));
                } else {
                    localPos.X -= 40f;
                    velocity = new Vector2(Rand.Range(-120f, -40f), Rand.Range(-2f, 15f));
                }
            }
            else // Hatch (Top/Bottom)
            {
                gapWorldPos.X += Rand.Range(-flowGap.Rect.Width * 0.4f, flowGap.Rect.Width * 0.4f);
                localPos = gapWorldPos - hullWorldPos;
                
                if (localPos.Y < 0) {
                    localPos.Y += 40f;
                    velocity = new Vector2(Rand.Range(-15f, 15f), Rand.Range(40f, 120f));
                } else {
                    localPos.Y -= 40f;
                    velocity = new Vector2(Rand.Range(-15f, 15f), Rand.Range(-120f, -40f));
                }
            }
        }
        else
        {
            localPos = new Vector2(Rand.Range(-halfWidth, halfWidth), Rand.Range(-halfHeight, halfHeight));
            velocity = new Vector2(Rand.Range(-5f, 5f), Rand.Range(2f, 8f));
        }
        
        var p = GetParticle();
        p.LocalPosition = localPos;
        p.Velocity = velocity;
        
        p.Size = Rand.Range(350f, 550f);
        
        p.Rotation = Rand.Range(0f, MathHelper.TwoPi);
        p.RotationSpeed = Rand.Range(-0.1f, 0.1f);
        p.Alpha = 0f;
        p.MaxAlpha = MathHelper.Lerp(0.1f, 0.65f, smokePct); // Linear density scaling
        p.Lifetime = 0f;
        p.MaxLifetime = Rand.Range(8f, 20f);
        
        targetList.Add(p);
    }

    private static double lastDrawTime = -1.0;

    public static void DrawFront_Prefix(SpriteBatch spriteBatch)
    {
        if ((object)Screen.Selected != GameMain.GameScreen || GameMain.GameSession == null) return;
        GameScreen gameScreen = GameMain.GameScreen;
        Camera cam = ((gameScreen != null) ? ((Screen)gameScreen).Cam : null);
        if (cam == null) return;

        double totalTime = Timing.TotalTime;
        if (lastDrawTime < 0.0) lastDrawTime = totalTime;
        float deltaTime = (float)(totalTime - lastDrawTime);
        lastDrawTime = totalTime;
        if (deltaTime > 0f && deltaTime < 0.5f)
        {
            UpdateParticles(deltaTime);
        }

        Texture2D tex = GetFogTexture();
        if (tex == null || hullParticles.Count == 0) return;

        if (!debugLogged)
        {
            Plugin.Log("SmokeRenderer: First frame of particles being drawn!");
            debugLogged = true;
        }

        spriteBatch.End();
        try
        {
            DrawParticles(spriteBatch, cam, tex);
        }
        finally
        {
            spriteBatch.Begin((SpriteSortMode)3, BlendState.NonPremultiplied, (SamplerState)null, DepthStencilState.None, (RasterizerState)null, (Effect)null, (Matrix?)cam.Transform);
        }
    }

    private static void DrawParticles(SpriteBatch spriteBatch, Camera cam, Texture2D tex)
    {
        Vector2 worldViewCenter = cam.WorldViewCenter;
        float num2 = (float)cam.WorldView.Width * 0.5f;
        float num3 = (float)cam.WorldView.Height * 0.5f;
        Vector2 origin = new Vector2((float)tex.Width * 0.5f, (float)tex.Height * 0.5f);
        
        GraphicsDevice graphicsDevice = ((GraphicsResource)spriteBatch).GraphicsDevice;
        Rectangle scissorRectangle = graphicsDevice.ScissorRectangle;
        Viewport viewport = graphicsDevice.Viewport;
        Rectangle bounds = viewport.Bounds;

        foreach (var kvp in hullParticles)
        {
            Hull val2 = kvp.Key;
            var particles = kvp.Value;
            if (particles.Count == 0 || ((Entity)val2).Removed) continue;

            Vector2 worldPosition = ((Entity)val2).WorldPosition;

            float num6 = ((MapEntity)val2).Rect.Width;
            float num7 = ((MapEntity)val2).Rect.Height;
            if (Math.Abs(worldPosition.X - worldViewCenter.X) > num2 + num6 || Math.Abs(worldPosition.Y - worldViewCenter.Y) > num3 + num7) continue;

            float num8 = ((val2.Volume > 0f) ? MathHelper.Clamp(1f - val2.WaterVolume / val2.Volume, 0f, 1f) : 1f);
            if (num8 < 0.02f) continue;

            float num9 = worldPosition.Y + num7 * 0.5f;
            float num10 = num9 - num7 * num8;
            Vector2 val3 = Vector2.Transform(new Vector2(worldPosition.X - num6 * 0.5f, 0f - num9), cam.Transform);
            Vector2 val4 = Vector2.Transform(new Vector2(worldPosition.X + num6 * 0.5f, 0f - num10), cam.Transform);
            
            Rectangle val5Screen = Rectangle.Intersect(new Rectangle((int)Math.Min(val3.X, val4.X), (int)Math.Min(val3.Y, val4.Y), (int)Math.Abs(val4.X - val3.X), (int)Math.Abs(val4.Y - val3.Y)), bounds);

            int inflateAmount = (int)(6f * cam.Zoom);
            val5Screen.Inflate(inflateAmount, inflateAmount);
            val5Screen = Rectangle.Intersect(val5Screen, bounds);
            
            if (val5Screen.Width > 0 && val5Screen.Height > 0)
            {
                graphicsDevice.ScissorRectangle = val5Screen;
                spriteBatch.Begin((SpriteSortMode)0, BlendState.NonPremultiplied, SamplerState.LinearClamp, (DepthStencilState)null, ScissorRasterizer, (Effect)null, (Matrix?)cam.Transform);

                for (int p = 0; p < particles.Count; p++)
                {
                    var particle = particles[p];
                    float scale = particle.Size / (float)tex.Width;
                    Color renderColor = new Color(SmokeColor.R, SmokeColor.G, SmokeColor.B, (int)(particle.Alpha * 255f));
                    
                    Vector2 drawPos = new Vector2(worldPosition.X + particle.LocalPosition.X, -(worldPosition.Y + particle.LocalPosition.Y));
                    
                    spriteBatch.Draw(tex, drawPos, null, renderColor, particle.Rotation, origin, scale, SpriteEffects.None, 0f);
                }

                spriteBatch.End();
            }
        }
        
        graphicsDevice.ScissorRectangle = scissorRectangle;
    }

    private static Texture2D? GetFogTexture()
    {
        if (fogTexture != null) return fogTexture;
        if (fogLoadFailed) return null;
        
        ContentPackage val = null;
        foreach (ContentPackage allPackage in ContentPackageManager.AllPackages)
        {
            if (allPackage.Name == "Bad Air")
            {
                val = allPackage;
                break;
            }
        }
        if (val == null)
        {
            fogLoadFailed = true;
            return null;
        }
        string text = System.IO.Path.Combine(val.Dir, "Content", "UI", "ba_smoke_fog.png");
        try
        {
            fogSpriteRef = new Sprite(text, Vector2.Zero);
            fogTexture = fogSpriteRef.Texture;
        }
        catch (Exception ex)
        {
            Plugin.Log("Could not load smoke fog texture: " + ex.Message);
            fogLoadFailed = true;
        }
        return fogTexture;
    }
}
#endif
