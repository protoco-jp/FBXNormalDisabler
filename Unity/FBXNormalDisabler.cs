#if UNITY_EDITOR

using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class NativePluginTest : MonoBehaviour
{
    const string DLL_NAME = "FBXNormalPlugin";

    // ネイティブ関数の宣言
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int AddTwoNumbers(int a, int b);

    void Start()
    {
        // 呼び出しテスト
        int result = AddTwoNumbers(5, 7);
        Debug.Log($"ネイティブプラグインからの応答: 5 + 7 = {result}");
    }
}

#endif