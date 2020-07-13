namespace EngineLightRelit
{
	/// <summary>
	/// A little single-purpose circular buffer to perform a rolling average on random values
	/// </summary>
	internal class JitterBuffer
	{
		private const int BUFFER_SIZE = 5;

		protected float[] buffer = new float[BUFFER_SIZE];
		protected int index = 0;

		public JitterBuffer()
		{
			this.Reset();
		}

		public void Reset()
		{
			for( int i = 0; i < BUFFER_SIZE; i++ )
			{
				this.buffer[this.index] = 0;
			}
		}

		public void NextJitter()
		{
			this.index++;
			this.index %= BUFFER_SIZE;
			this.buffer[this.index] = UnityEngine.Random.value;
		}

		public float GetAverage( bool autoJitter = true )
		{
			if( autoJitter )
			{
				this.NextJitter();
			}
			float avg = 0;
			for( int i = 0; i < BUFFER_SIZE; i++ )
			{
				avg += this.buffer[i];
			}
			return avg / BUFFER_SIZE;
		}
	}
}