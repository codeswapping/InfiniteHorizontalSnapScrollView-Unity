using InfiniteHorizontalSnapScrollView.Scripts;
using UnityEditor;
using UnityEngine;

namespace InfiniteHorizontalSnapScrollView.Editor
{
    [CustomEditor(typeof(InfiniteSnapScrollView))]
    public class InfiniteScrollSnapEditor : UnityEditor.Editor
    {
        private InfiniteSnapScrollView _target;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if(_target == null) _target = (InfiniteSnapScrollView) target;
        }

        private void OnSceneGUI()
        {
            if(Application.isPlaying) return;
            Debug.Log("Called from SceneGUI"); 
            if(_target == null) _target = (InfiniteSnapScrollView) target;
            _target.OnUpdateLayout();
         }

        private void Reset()
        {
            if(Application.isPlaying) return;
            //Debug.Log("Called from Reset");
            if(_target == null) _target = (InfiniteSnapScrollView) target;
            _target.OnUpdateLayout();
        }
    }
}
