using System;
using System.Collections.Generic;
using UnityEngine;

namespace EngineLightRelit
{
	/// <summary>
	/// Legacy thing, but still helpful.
	/// </summary>
	internal class EngineModuleWrapper
	{
		//public bool hasEmissive { get; protected set; }
		public Transform transform { get { return engineModules[0].transform; } } // hope this works...
		public bool isEnabled { get { return engineModules[0].isEnabled; } }

		protected ModuleEngines[] engineModules;

		//protected FXModuleAnimateThrottle engineEmissiveModule;

		public EngineModuleWrapper( Part part )
		{
			// RAPIERs have two engine modules - they share an emissive texture, but not a throttle value
			this.engineModules = part.FindModulesImplementing<ModuleEngines>().ToArray(); // do it once, do it in the right place
			if( this.engineModules.Length < 1 )
			{
				throw new Exception( "could not locate an engine on part: " + part.name );
			}
			foreach( var module in this.engineModules )
			{
				if( module == null ) // this is how much I trust the KSP API...
				{
					throw new Exception( "could not locate an engine on part: " + part.name );
				}
			}
		}

		public float GetThrottle()
		{
			if( this.engineModules.Length == 1 )
			{
				return this.engineModules[0].currentThrottle;
			}
			else
			{
				// return the greatest throttle setting of available engine modules
				float throttle = 0;
				for( int i = 0; i < this.engineModules.Length; i++ )
				{
					throttle = (this.engineModules[i].currentThrottle > throttle) ? this.engineModules[i].currentThrottle : throttle;
				}

				return throttle;
			}
		}

		public float GetMaxThrust( bool fromEnabledModulesOnly = false )
		{
			/* this approach means multimode engines with very disparate thrust ratings will have
			 * incorrect light intensity when they switch to low-thrust mode, possibly on startup
			 * acceptible for now...
			 * set the optional parameter when doing recalculation for mode-switching */
			if( this.engineModules.Length == 1 )
			{
				return this.engineModules[0].maxThrust;
			}
			else
			{
				// return the greatest thrust setting of (operational) engine modules
				float thrust = 0;
				for( int i = 0; i < this.engineModules.Length; i++ )
				{
					if( fromEnabledModulesOnly && !this.engineModules[i].isEnabled )
					{
						continue; // skip disabled modules
					}
					thrust = (this.engineModules[i].maxThrust > thrust) ? this.engineModules[i].maxThrust : thrust;
				}

				return thrust;
			}
		}

		/// <summary>
		/// Returns global position and global forward dir for every thrust transform of the given engine
		/// </summary>
		public List<Tuple<Vector3, Vector3>> GetThrustTransforms()
		{
			List<Tuple<Vector3, Vector3>> ret = new List<Tuple<Vector3, Vector3>>();

			for( int i = 0; i < this.engineModules.Length; i++ )
			{
				for( int j = 0; j < this.engineModules[i].thrustTransforms.Count; j++ )
				{
					Transform t = this.engineModules[i].thrustTransforms[j];

					ret.Add( new Tuple<Vector3, Vector3>( t.position, t.forward ) );
				}
			}

			return ret;
		}
	}
}