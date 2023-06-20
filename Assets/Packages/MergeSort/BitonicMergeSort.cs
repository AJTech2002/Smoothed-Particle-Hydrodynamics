using UnityEngine;
using System.Collections;

namespace MergeSort {
	public class BitonicMergeSort : System.IDisposable {
		public const string KERNEL_SORT = "BitonicSort";
		public const string KERNEL_SORT_INT = "BitonicSortInt";
		public const string KERNEL_INIT = "InitKeys";

		public const string PROP_BLOCK = "block";
		public const string PROP_DIM = "dim";
		public const string PROP_COUNT = "count";

		public const string BUF_KEYS = "Keys";
		public const string BUF_VALUES = "Values";
		public const string BUF_INT_VALUES = "IntValues";

		readonly ComputeShader _compute;
		readonly int _kernelSort, _kernelSortInt, _kernelInit;

		public BitonicMergeSort(ComputeShader compute) {
			_compute = compute;
			_kernelInit = compute.FindKernel(KERNEL_INIT);
			_kernelSort = compute.FindKernel(KERNEL_SORT);
			_kernelSortInt = compute.FindKernel(KERNEL_SORT_INT);
		}

		public void Init(ComputeBuffer keys) {
			int x, y, z;
			ShaderUtil.CalcWorkSize(keys.count, out x, out y, out z);
			_compute.SetInt(PROP_COUNT, keys.count);
			_compute.SetBuffer(_kernelInit, BUF_KEYS, keys);
			_compute.Dispatch(_kernelInit, x, y, z);
		}
		public void Sort(ComputeBuffer keys, ComputeBuffer values) {
			var count = keys.count;
			int x, y, z;
			ShaderUtil.CalcWorkSize(count, out x, out y, out z);

			_compute.SetInt(PROP_COUNT, count);
			for (var dim = 2; dim <= count; dim <<= 1) {
				_compute.SetInt(PROP_DIM, dim);
				for (var block = dim >> 1; block > 0; block >>= 1) {
					_compute.SetInt(PROP_BLOCK, block);
					_compute.SetBuffer(_kernelSort, BUF_KEYS, keys);
					_compute.SetBuffer(_kernelSort, BUF_VALUES, values);
					_compute.Dispatch(_kernelSort, x, y, z);
				}
			}
		}
		public void SortInt(ComputeBuffer keys, ComputeBuffer values) {
			var count = keys.count;
			int x, y, z;
			ShaderUtil.CalcWorkSize(count, out x, out y, out z);
			
			_compute.SetInt(PROP_COUNT, count);
			for (var dim = 2; dim <= count; dim <<= 1) {
				_compute.SetInt(PROP_DIM, dim);
				for (var block = dim >> 1; block > 0; block >>= 1) {
					_compute.SetInt(PROP_BLOCK, block);
					_compute.SetBuffer(_kernelSortInt, BUF_KEYS, keys);
					_compute.SetBuffer(_kernelSortInt, BUF_INT_VALUES, values);
					_compute.Dispatch(_kernelSortInt, x, y, z);
				}
			}
		}

		#region IDisposable implementation
		public void Dispose () {
		}
		#endregion
	}
}