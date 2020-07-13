using UnityEngine;

namespace EngineLightRelit
{
	internal static class Utils
	{
		//As the name says, checks if the user is using the IVA camera
		//Added the OR just in case that other camera mode is also IVA!
		public static bool IsIVA()
		{
			return (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA) || (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal);
		}

		public static void Log( string text )
		{
			Debug.Log( "[EngineLight] " + text );
		}
	}
}