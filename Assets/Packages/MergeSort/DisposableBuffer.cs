using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;

namespace MergeSort {
	public class DisposableBuffer<T> : System.IDisposable {
		public ComputeBuffer Buffer { get; private set; }
		public T[] Data { get; private set; }

		public DisposableBuffer(int capacity) {
			Data = new T[capacity];
			Buffer = new ComputeBuffer(capacity, Marshal.SizeOf(typeof(T)));
			Upload();
		}

		public void Upload() { Buffer.SetData(Data); }
		public T[] Download() { Buffer.GetData(Data); return Data; }

		#region IDisposable implementation
		public void Dispose () {
			if (Buffer != null)
				Buffer.Dispose();
		}
		#endregion
	}
}