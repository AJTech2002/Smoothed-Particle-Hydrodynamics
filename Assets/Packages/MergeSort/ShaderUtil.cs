using UnityEngine;
using System.Collections;


namespace MergeSort {
	public static class ShaderUtil {
		public const int GROUP_SIZE = 256;
		public const int MAX_DIM_GROUPS = 1024;
		public const int MAX_DIM_THREADS = (GROUP_SIZE * MAX_DIM_GROUPS);

		public static void CalcWorkSize(int length, out int x, out int y, out int z) {
			if (length <= MAX_DIM_THREADS) {
				x = (length - 1) / GROUP_SIZE + 1;
				y = z = 1;
			} else {
				x = MAX_DIM_GROUPS;
				y = (length - 1) / MAX_DIM_THREADS + 1;
				z = 1;
			}
			//Debug.LogFormat("WorkSize {0}x{1}x{2}", x, y, z);
		}
		public static int AlignBufferSize(int length) {
			return ((length - 1) / GROUP_SIZE + 1) * GROUP_SIZE;
		}
	}
}
