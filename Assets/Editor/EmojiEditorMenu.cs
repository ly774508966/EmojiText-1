using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public static class EmojiEditorMenu
{
    [MenuItem("Assets/Create/Emoji Asset",false,10)]
    static void CreateEmojiAsset()
    {
        Object target = Selection.activeObject;
        if (target == null || target.GetType() != typeof(Texture2D))
            return;

        Texture2D sourceTex = target as Texture2D;
        //整体路径
        string filePathWithName = AssetDatabase.GetAssetPath(sourceTex);
        //带后缀的文件名
        string fileNameWithExtension = Path.GetFileName(filePathWithName);
        //不带后缀的文件名
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePathWithName);
        //不带文件名的路径
        string filePath = filePathWithName.Replace(fileNameWithExtension, "");

        EmojiAsset spriteAsset = AssetDatabase.LoadAssetAtPath(filePath + fileNameWithoutExtension + ".asset", typeof(EmojiAsset)) as EmojiAsset;
        bool isNewAsset = spriteAsset == null ? true : false;
        if (isNewAsset)
        {
            spriteAsset = ScriptableObject.CreateInstance<EmojiAsset>();
            spriteAsset.texSource = sourceTex;
            spriteAsset.listSpriteGroup = GetAssetSpriteInfor(sourceTex);
            AssetDatabase.CreateAsset(spriteAsset, filePath + fileNameWithoutExtension + ".asset");
        }
    }

    [MenuItem("GAME/UI/Emoji Text", false, 10)]
    static void CreateEmojiText(MenuCommand menuCommand)
    {
        return;
        GameObject go = null;
        //EmojiManager _inline = AssetDatabase.LoadAssetAtPath<EmojiManager>("Assets/EmojiTextEditorMenu/Prefabs/Emoji Text.prefab");
        //if (_inline)
        //{
        //    go = GameObject.Instantiate(_inline).gameObject;
        //}
        //else
        {
            go = new GameObject();
            go.AddComponent<EmojiText>();
        }
        go.name = "Emoji Text";
        GameObject _parent = menuCommand.context as GameObject;
        if (_parent == null)
        {
            _parent = new GameObject("Canvas");
            _parent.layer = LayerMask.NameToLayer("UI");
            _parent.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _parent.AddComponent<CanvasScaler>();
            _parent.AddComponent<GraphicRaycaster>();

            EventSystem _es = GameObject.FindObjectOfType<EventSystem>();
            if (!_es)
            {
                _es = new GameObject("EventSystem").AddComponent<EventSystem>();
                _es.gameObject.AddComponent<StandaloneInputModule>();
            }
        }
        GameObjectUtility.SetParentAndAlign(go, _parent);
        //注册返回事件
        Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
        Selection.activeObject = go;
    }

    private static List<SpriteInfoGroup> GetAssetSpriteInfor(Texture2D tex)
    {
        List<SpriteInfoGroup> _listGroup = new List<SpriteInfoGroup>();
        string filePath = UnityEditor.AssetDatabase.GetAssetPath(tex);

        Object[] objects = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(filePath);

        List<SpriteInfo> _tempSprite = new List<SpriteInfo>();

        Vector2 _texSize = new Vector2(tex.width, tex.height);
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i].GetType() != typeof(Sprite))
                continue;
                SpriteInfo temp = new SpriteInfo();
                Sprite sprite = objects[i] as Sprite;
                temp.ID = i;
                temp.name = sprite.name;
                temp.pivot = sprite.pivot;
                temp.rect = sprite.rect;
                temp.sprite = sprite;
                temp.tag = sprite.name;
                temp.uv = GetSpriteUV(_texSize, sprite.rect);
                 _tempSprite.Add(temp);
        }

        for (int i = 0; i < _tempSprite.Count; i++)
        {
            SpriteInfoGroup _tempGroup = new SpriteInfoGroup();
            _tempGroup.tag = _tempSprite[i].tag;
            //_tempGroup.size = 24.0f;
            //_tempGroup.width = 1.0f;
            _tempGroup.listSpriteInfo = new List<SpriteInfo>();
            _tempGroup.listSpriteInfo.Add(_tempSprite[i]);
            for (int j = i+1; j < _tempSprite.Count; j++)
            {
                if ( _tempGroup.tag == _tempSprite[j].tag)
                {
                    _tempGroup.listSpriteInfo.Add(_tempSprite[j]);
                    _tempSprite.RemoveAt(j);
                    j--;
                }
            }
            _listGroup.Add(_tempGroup);
            _tempSprite.RemoveAt(i);
            i--;
        }

        return _listGroup;
    }

    private static Vector2[] GetSpriteUV(Vector2 texSize,Rect _sprRect)
    {
        Vector2[] uv = new Vector2[4];
        uv[0] = new Vector2(_sprRect.x / texSize.x, (_sprRect.y+_sprRect.height) / texSize.y);
        uv[1] = new Vector2((_sprRect.x + _sprRect.width) / texSize.x, (_sprRect.y +_sprRect.height) / texSize.y);
        uv[2] = new Vector2((_sprRect.x + _sprRect.width) / texSize.x, _sprRect.y / texSize.y);
        uv[3] = new Vector2(_sprRect.x / texSize.x, _sprRect.y / texSize.y);
        return uv;
    }
    
}
