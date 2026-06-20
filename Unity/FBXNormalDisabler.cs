#if UNITY_EDITOR

using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEditor;

public class FBXNormalDisabler : MonoBehaviour
{
    const string DLL_NAME = "FBXNormalPlugin";

    [StructLayout(LayoutKind.Sequential)]
    public struct ShapeName {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string name;
    }

    // ネイティブ関数の宣言
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int DisableFBXShapeNormal(
        [MarshalAs(UnmanagedType.LPStr)] string path,
        uint pathLen,
        [In] ShapeName[] shapeNames,
        uint shapeCount
    );

    [MenuItem("Tools/Disable Normals")]
    private static void DisableNormals()
    {
        // 呼び出しテスト
        int result = DisableFBXShapeNormal(
            "D:/unity/Ruidas/Assets/PROTOCO/Avatar/Ruidas/FBX/Ruidas.fbx",
            (uint)"D:/unity/Ruidas/Assets/PROTOCO/Avatar/Ruidas/FBX/Ruidas.fbx".Length,
            new ShapeName[] { new ShapeName { name = "Shape1" }, new ShapeName { name = "Shape2" } },
            2
        );
        Debug.Log($"ネイティブプラグインからの応答: = {result}");
    }
}

#endif