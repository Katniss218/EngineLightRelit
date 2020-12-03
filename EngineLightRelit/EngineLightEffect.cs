/*
	Copyright (c) 2016

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

 *   Original author:
 *   - Tatjam (Tajampi)
 *   Original contributors:
 *   - Saabstory88
 *   - SpaceTiger (aka Xarun)
 *   - ToXik-yogHurt (code refactoring asshole)
 *   
 *########################################################################################
 *   
 *   EngineLightKatnissified mod for KSP RSS/RO
 *   
 *   Adapted for RSS/RO by Katniss (Katniss218)
 *   
 *########################################################################################
 */

using System;
using System.Collections.Generic;
using UnityEngine;


namespace EngineLightRelit
{
	public class EngineLightEffect : PartModule
	{
		public const float LIGHT_CURVE = -0.0000004f;
		public const float LIGHT_LINEAR = 0.0068f;
		public const float LIGHT_MINIMUM = 0.1304f;

		public const float NOZZLE_LIGHT_RANGE_MULTIPLIER = 0.75f;
		public const float AREA_LIGHT_RANGE_MULTIPLIER = 1.25f;
		public const float NOZZLE_LIGHT_INTENSITY_MULTIPLIER = 1.15f;
		public const float AREA_LIGHT_INTENSITY_MULTIPLIER = 1f;

		// Variables:

		/* CAUTION - don't modify these fields at runtime and expect much to happen */
		// on start many of these are used to initialise internal Color structs and then never read again

		[KSPField] public float lightPower = 1.0f;

		[KSPField] public float lightRange = 9f;

		[KSPField] public float plumeLength = 20f;

		[KSPField] public float exhaustRed = 1.0f;

		[KSPField] public float exhaustGreen = 0.88f;

		[KSPField] public float exhaustBlue = 0.68f;

		[KSPField] public float jitterMultiplier = 0.1f;

		[KSPField] public float multiplierOnIva = 0.5f;
		
		// allow manual tweaking of the light location
		[KSPField] public float exhaustOffsetZ = 0;
		
		[KSPField] public float lightFadeCoefficient = 0.8f; // keep less than 1	


		protected bool initOccurred = false; // this is a bit icky - but it's just for debugging

		protected Tuple<Light, Light>[] lightStacks = null;

		protected LightStates lightState = LightStates.Disabled;
		protected LightStates lastFrameLightState = LightStates.Disabled;

		EngineModuleWrapper engine;

		JitterBuffer jitterBuffer;
		
		Color exhaustColor;
		
		double lastReportTime = 0;
		float jitteredThrottle = 0;
		float lastFrameThrottle = 0;
		float multiTransformIntensityMult = 1f;

		private float GetIntensityMult( int thrustTransformCount )
		{
			if( thrustTransformCount <= 1 )
			{
				return 1f;
			}
			if( thrustTransformCount > 8 )
			{
				return 0.5f;
			}
			else
			{
				// start at 1, go towards 0.5 intensity at "infinity".
				float acc = 1f;
				float step = 0.25f;

				for( int i = 0; i < thrustTransformCount; i++ )
				{
					acc -= step;

					step *= 0.5f;
				}

				return acc;
			}
		}

		private Tuple<Light, Light> MakeLightStack( Vector3 position, Color color, Vector3 thrustTransformForward, float exhaustOffsetZ )
		{
			// spawn 2 lights, one point (top) and one spot (top-ish, pointing down)

			// light1 - nozzle light
			// this exists so the light is stretched vertically, and it also illuminates the rocket better.
			
			GameObject gameObject1 = new GameObject( "_EngineLight_Nozzle" );

			Light light1 = gameObject1.AddComponent<Light>();
			light1.enabled = false;

			// Light Settings
			light1.type = LightType.Spot;
			light1.spotAngle = 120;
			light1.color = color;

			// Transform Settings
			gameObject1.transform.parent = this.engine.transform;
			gameObject1.transform.forward = thrustTransformForward;
			gameObject1.transform.position = position;
			gameObject1.transform.Translate( new Vector3( 0, 0, exhaustOffsetZ ), Space.Self );

			// light2 - area (big) light

			GameObject gameObject2 = new GameObject( "_EngineLight_Area" );

			Light light2 = gameObject2.AddComponent<Light>();
			light2.enabled = false;
			light2.shadows = LightShadows.Hard;
			light2.shadowStrength = 1;

			// Light Settings
			light2.type = LightType.Point;
			light2.color = color;

			// Transform Settings
			gameObject2.transform.parent = this.engine.transform;
			gameObject2.transform.forward = thrustTransformForward;
			gameObject2.transform.position = position;
			// Move our area light down a bit compared to the nozzle light
			gameObject2.transform.Translate( new Vector3( 0, 0, exhaustOffsetZ + (this.plumeLength * 0.25f) ), Space.Self );

#if DEBUG
			Utils.Log( gameObject1.transform.position.ToString() );
			Utils.Log( gameObject2.transform.position.ToString() );
#endif

			return new Tuple<Light, Light>( light1, light2 );
		}

		public void InitEngineLights()
		{
			try
			{
				// wrap the parts engine module(s) and FX modules for simpler calls later	        
				this.engine = new EngineModuleWrapper( this.part );
				
				this.exhaustColor = new Color( this.exhaustRed, this.exhaustGreen, this.exhaustBlue );

				jitterBuffer = new JitterBuffer();

				// cache function call
				float maxThrust = engine.GetMaxThrust();

				// calculate light power from engine max thrust - follows a quadratic:
				//this.lightPower = ((LIGHT_CURVE * maxThrust * maxThrust)
				//			+ (LIGHT_LINEAR * maxThrust)
				//			+ LIGHT_MINIMUM)
				//			* this.lightPower; // use the multiplier read from config file



				//Make lights: (Using part position)

				List<Tuple<Vector3, Vector3>> thrustTransforms = this.engine.GetThrustTransforms();

				this.lightStacks = new Tuple<Light, Light>[thrustTransforms.Count];

				for( int i = 0; i < thrustTransforms.Count; i++ )
				{
					Tuple<Light, Light> lights = this.MakeLightStack( thrustTransforms[i].Item1, exhaustColor, thrustTransforms[i].Item2, this.exhaustOffsetZ );

					this.lightStacks[i] = lights;
				}

				this.multiTransformIntensityMult = this.GetIntensityMult( thrustTransforms.Count );

				this.initOccurred = true;



				// this is how you do debug only printing...	
#if DEBUG
				Utils.Log( "Light calculations (" + this.part.name + ") resulted in: " + lightPower );
				Utils.Log( "coords of engine: " + engine.transform.position );
#else
	Utils.Log("Detected and activating for engine: (" + this.part.name + ")");
#endif
			}
			catch( Exception exception )
			{
				Utils.Log( "Error in init(): " + exception.Message );
			}
		}


		public void Start() // doesn't get called for reverting to launch??
		{
			if( !HighLogic.LoadedSceneIsFlight || this.initOccurred )
			{
				return; //Beware the bugs!
			}

#if DEBUG
			Utils.Log( "Initialized part (" + this.part.partName + ") Proceeding to patch!" );
#endif

			InitEngineLights(); // allows manual init / re-init of module, probably

		}

		public void FixedUpdate()
		{
			if( !HighLogic.LoadedSceneIsFlight )
			{
				return;
			}
			if( !this.initOccurred )
			{
#if DEBUG
				Utils.Log( "FixedUpdate() called before Start() - wtf even?" ); // lul
#endif
				return;
			}

			try
			{
				// these _really_ shouldn't be happening - if one does we need to fix it, not ignore it. 
				// the performance hit from throwing exceptions should encourage that.
				if( this.lightStacks == null )
				{
					throw new Exception( "LightStacks failed to initialize correctly." );
				}
				if( this.engine == null )
				{
					throw new Exception( "EngineModule failed to initialize correctly." );
				}

				bool isIVA = Utils.IsIVA();

				float throttle = engine.GetThrottle(); // cache this - don't trust KSPs properties
				
				// smooth the drop-off in intensity from sudden throttle decreases
				// the exhaust is still somewhat present, and that's what's supposed to be emitting the light
				if( this.lastFrameThrottle > 0 && (this.lastFrameThrottle - throttle / this.lastFrameThrottle) > (1 - this.lightFadeCoefficient) )
				{
					throttle = this.lastFrameThrottle * this.lightFadeCoefficient;
				}

				// d'awww, it's a wee finite state machine!
				this.lightState = LightStates.Disabled;
				if( throttle > 0 )
				{
					this.lightState = this.lightState | LightStates.Exhaust;
				}

				for( int i = 0; i < this.lightStacks.Length; i++ )
				{
					if( MapView.MapIsEnabled || (isIVA && this.multiplierOnIva < 0.1f) || this.lightStacks[i].Item1.intensity < 0.1 || this.lightStacks[i].Item2.intensity < 0.1 )
					{
						this.lightStacks[i].Item1.enabled = false;
						this.lightStacks[i].Item2.enabled = false;
					}
					else
					{
						this.lightStacks[i].Item1.enabled = true;
						this.lightStacks[i].Item2.enabled = true;
					}

					switch( this.lightState )
					{
						case LightStates.Exhaust:
							this.lightStacks[i].Item1.color = this.exhaustColor; // when restarting an engine
							this.lightStacks[i].Item2.color = this.exhaustColor; // when restarting an engine
							this.SetIntensityFromExhaust( throttle, i );
							
							if( isIVA )
							{
								this.lightStacks[i].Item1.intensity *= this.multiplierOnIva;
								this.lightStacks[i].Item2.intensity *= this.multiplierOnIva;
							}
							break;

						case LightStates.Disabled:
							this.lightStacks[i].Item1.enabled = false;
							this.lightStacks[i].Item2.enabled = false;

							break;
					}

					//Prevents light from reaching the planet while in space
					int mask = ~(1 << vessel.mainBody.scaledBody.layer);

					this.lightStacks[i].Item1.cullingMask &= mask;
					this.lightStacks[i].Item2.cullingMask &= mask;
				}
#if DEBUG
					if( lastReportTime < Time.time )
					{
						if( engine.isEnabled )
						{
							Utils.Log( "part: " + part.name );
							Utils.Log( "fade rate: " + lightFadeCoefficient );
							Utils.Log( "lightstate: " + lightState );
							Utils.Log( "throttle: " + throttle );
							Utils.Log( "jittered throttle: " + jitteredThrottle );
							Utils.Log( "previous throttle: " + lastFrameThrottle );
							Utils.Log( "coords of engine: " + engine.transform.position );
							Utils.Log( "" );
						}
						lastReportTime = Time.time + 1;
					}
#endif
				
				this.lastFrameThrottle = throttle;
			}
			catch( Exception exception )
			{
				Utils.Log( "Error in FixedUpdate(): " + exception.Message );
			}
		}


		// the part of me that values readability wants these functions out here, the part of me concerned
		// with performance wants to find a way to inline them - neatness wins for today

		protected void SetIntensityFromExhaust( float throttle, int i )
		{
			//this is how we keep the maths to a minimum
			jitteredThrottle = throttle + jitterBuffer.GetAverage() * jitterMultiplier; // per-frame jitter was annoying, now it's smoothed

			float intensity = lightPower * jitteredThrottle * jitteredThrottle * this.multiTransformIntensityMult; // exponential increase in intensity with throttle

			this.lightStacks[i].Item1.intensity = intensity * NOZZLE_LIGHT_INTENSITY_MULTIPLIER;
			this.lightStacks[i].Item2.intensity = intensity * AREA_LIGHT_INTENSITY_MULTIPLIER;

			float range = lightRange * jitteredThrottle; // linear increase in range with throttle

			this.lightStacks[i].Item1.range = range * NOZZLE_LIGHT_RANGE_MULTIPLIER; // linear increase in range with throttle
			this.lightStacks[i].Item2.range = range * AREA_LIGHT_RANGE_MULTIPLIER; // linear increase in range with throttle
		}

		/*protected void SetIntensityFromEmissive( int i )
		{
			// harder maths than the main engine light (ironically?)

			// Log10 is nice mathematically, we stay in pretty much the same range of values
			// but get a boost in intensity at low emisssitivity values
			this.emissiveIntensity = (float)(1 + Math.Log10( this.emissiveValue ));

			// shift the color from red towards yellow (white?) as the emissive increases
			// summing a logarithm and a square over the range 0.1 -> 1 gives a roughly linear output
			// splitting the emissive into two modifiers lets you fade the colour channels up slightly-independantly
			// it's not as flexible as 3 separate-channel polynomial curves, but it's also much faster to run real-time
			this.emissiveColor = this.emissiveColorBase;
			this.emissiveColor += this.emissiveValue * this.emissiveValue * this.emissiveColorQuadModifier;
			this.emissiveColor += this.emissiveIntensity * this.emissiveColorLogModifier;

			// spread the color out between exhaust light and emissive light
			// just using an average causes emissive to overpower exhaust light, weight the colour balance towards the active light
			this.emissiveIntensity *= this.engineEmissiveMultiplier;

			Color color1 = (this.exhaustColor * this.lightStacks[i].Item1.intensity * 3) + (this.emissiveColor * this.emissiveIntensity);
			color1 /= ((this.lightStacks[i].Item1.intensity * 3) + this.emissiveIntensity);

			Color color2 = (this.exhaustColor * this.lightStacks[i].Item2.intensity * 3) + (this.emissiveColor * this.emissiveIntensity);
			color2 /= ((this.lightStacks[i].Item2.intensity * 3) + this.emissiveIntensity);

			this.lightStacks[i].Item1.color = color1;
			this.lightStacks[i].Item2.color = color2;
			this.lightStacks[i].Item1.intensity += emissiveIntensity;
			this.lightStacks[i].Item2.intensity += emissiveIntensity;
		}*/
	}
}