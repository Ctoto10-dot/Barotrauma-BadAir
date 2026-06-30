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
		private const float MinSmoke = 4f;

		private const float FogFullSmoke = 42f;

		private const float FadeSpeed = 2.5f;

		private static readonly Color SmokeLight = new Color(140, 142, 150);

		private static readonly Color SmokeDark = new Color(95, 97, 108);

		private static readonly float[] BaseCover = new float[3] { 1.8f, 2.2f, 2.6f };

		private static readonly float[] BaseDrift = new float[3] { 0.11f, -0.07f, 0.05f };

		private static readonly float[] BaseRotate = new float[3] { 0.04f, -0.03f, 0.05f };

		private static readonly float[] BaseAlpha = new float[3] { 0.64f, 0.52f, 0.42f };

		private static readonly ConditionalWeakTable<Hull, StrongBox<float>> rendered = new ConditionalWeakTable<Hull, StrongBox<float>>();

		private static Texture2D? fogTexture;
		private static Sprite fogSpriteRef;

		private static bool fogLoadFailed;

		private static readonly RasterizerState ScissorRasterizer = new RasterizerState
		{
			ScissorTestEnable = true,
			CullMode = (CullMode)0
		};

		private static double lastTime = -1.0;

		public static void DrawFront_Prefix(SpriteBatch spriteBatch)
		{
			
			if ((object)Screen.Selected != GameMain.GameScreen || GameMain.GameSession == null)
			{
				return;
			}
			GameScreen gameScreen = GameMain.GameScreen;
			Camera val = ((gameScreen != null) ? ((Screen)gameScreen).Cam : null);
			if (val == null)
			{
				return;
			}
			Texture2D val2 = GetFogTexture();
			if (val2 == null)
			{
				return;
			}
			spriteBatch.End();
			try
			{
				DrawFog(spriteBatch, val, val2);
			}
			finally
			{
				spriteBatch.Begin((SpriteSortMode)3, BlendState.NonPremultiplied, (SamplerState)null, DepthStencilState.None, (RasterizerState)null, (Effect)null, (Matrix?)val.Transform);
			}
		}

		private static void DrawFog(SpriteBatch spriteBatch, Camera cam, Texture2D tex)
		{
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			
			float num = MathHelper.Clamp(2.5f * GetDeltaTime(), 0f, 1f);
			Vector2 worldViewCenter = cam.WorldViewCenter;
			float num2 = (float)cam.WorldView.Width * 0.5f;
			float num3 = (float)cam.WorldView.Height * 0.5f;
			Vector2 val = new Vector2((float)tex.Width * 0.5f, (float)tex.Height * 0.5f);
			
			float num4 = (float)Timing.TotalTime;
			GraphicsDevice graphicsDevice = ((GraphicsResource)spriteBatch).GraphicsDevice;
			Rectangle scissorRectangle = graphicsDevice.ScissorRectangle;
			Viewport viewport = graphicsDevice.Viewport;
			Rectangle bounds = viewport.Bounds;
			List<Hull> hullList = Hull.HullList;
			Vector2 val6 = default(Vector2);
			Vector2 val8 = default(Vector2);
			for (int i = 0; i < hullList.Count; i++)
			{
				Hull val2 = hullList[i];
				float num5 = 0f;
				if (HullAtmosphere.TryGet(val2, out HullAtmosphere atmosphere) && atmosphere.Smoke > 4f)
				{
					num5 = MathHelper.Clamp((atmosphere.Smoke - 4f) / 38f, 0f, 1f);
				}
				StrongBox<float> value;
				bool flag = rendered.TryGetValue(val2, out value);
				if (num5 <= 0f && !flag)
				{
					continue;
				}
				if (!flag)
				{
					value = new StrongBox<float>(0f);
					rendered.Add(val2, value);
				}
				value.Value += (num5 - value.Value) * num;
				float value2 = value.Value;
				if (value2 < 0.012f)
				{
					continue;
				}
				Vector2 worldPosition = ((Entity)val2).WorldPosition;
				float num6 = ((MapEntity)val2).Rect.Width;
				float num7 = ((MapEntity)val2).Rect.Height;
				if (Math.Abs(worldPosition.X - worldViewCenter.X) > num2 + num6 || Math.Abs(worldPosition.Y - worldViewCenter.Y) > num3 + num7)
				{
					continue;
				}
				float num8 = ((val2.Volume > 0f) ? MathHelper.Clamp(1f - val2.WaterVolume / val2.Volume, 0f, 1f) : 1f);
				if (num8 < 0.02f)
				{
					continue;
				}
				float num9 = worldPosition.Y + num7 * 0.5f;
				float num10 = num9 - num7 * num8;
				float num11 = num9 - num7 * num8 * 0.5f;
				Vector2 val3 = Vector2.Transform(new Vector2(worldPosition.X - num6 * 0.5f, 0f - num9), cam.Transform);
				Vector2 val4 = Vector2.Transform(new Vector2(worldPosition.X + num6 * 0.5f, 0f - num10), cam.Transform);
				Rectangle val5 = Rectangle.Intersect(new Rectangle((int)Math.Min(val3.X, val4.X), (int)Math.Min(val3.Y, val4.Y), (int)Math.Abs(val4.X - val3.X), (int)Math.Abs(val4.Y - val3.Y)), bounds);
				if (val5.Width > 0 && val5.Height > 0)
				{
					graphicsDevice.ScissorRectangle = val5;
					spriteBatch.Begin((SpriteSortMode)0, BlendState.NonPremultiplied, SamplerState.LinearClamp, (DepthStencilState)null, ScissorRasterizer, (Effect)null, (Matrix?)cam.Transform);
					val6 = new Vector2(worldPosition.X, 0f - num11);
					float num12 = Math.Max(num6, num7);
					float num13 = num7 * num8;
					Color val7 = Color.Lerp(SmokeLight, SmokeDark, value2);
					float num14 = num6 * 0.12f;
					float num15 = num13 * 0.12f;
					for (int j = 0; j < BaseCover.Length; j++)
					{
						float num16 = (float)j * 2.3f;
						val8 = new Vector2((float)Math.Sin(num4 * BaseDrift[j] + num16) * num14, (float)Math.Cos(num4 * BaseDrift[j] * 0.8f + num16) * num15);
						float num17 = num12 * BaseCover[j] / (float)tex.Width;
						spriteBatch.Draw(tex, val6 + val8, (Rectangle?)null, val7 * (value2 * BaseAlpha[j]), num4 * BaseRotate[j], val, num17, (SpriteEffects)0, 0f);
					}
					spriteBatch.End();
				}
			}
			graphicsDevice.ScissorRectangle = scissorRectangle;
		}

		private static float GetDeltaTime()
		{
			double totalTime = Timing.TotalTime;
			if (lastTime < 0.0)
			{
				lastTime = totalTime;
				return 0f;
			}
			float num = (float)(totalTime - lastTime);
			lastTime = totalTime;
			if (!(num > 0f) || !(num < 0.5f))
			{
				return 0f;
			}
			return num;
		}

		private static Texture2D? GetFogTexture()
		{
			if (fogTexture != null)
			{
				return fogTexture;
			}
			if (fogLoadFailed)
			{
				return null;
			}
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

