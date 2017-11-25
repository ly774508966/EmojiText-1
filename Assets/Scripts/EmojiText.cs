using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Text.RegularExpressions;
using System.Text;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class EmojiText : Text, IPointerClickHandler
{
    public EmojiAsset m_EmojiAsset;
    //动画速度
    public float m_EmojiSpeed = 5.0f;

    //所有的精灵消息
    public static Dictionary<int, Dictionary<string, SpriteInfoGroup>> _IndexSpriteInfo;
    // 用正则取  [图集ID#表情Tag] ID值==-1 ,表示为超链接
    private static readonly Regex _InputTagRegex = new Regex(@"\[(\-{0,1}\d{0,})#(.+?)\]", RegexOptions.Singleline);
    //更新后的文本
    private string _OutputText = "";
    //表情位置索引信息
    Dictionary<int, SpriteTagInfo> _SpriteInfo;
    //图集ID，相关信息
    Dictionary<int, List<SpriteTagInfo>> _DrawSpriteInfo;

    private Mesh _mesh;
    
    //绘制的模型数据索引
    private Dictionary<int, MeshInfo> _TextMeshInfo = new Dictionary<int, MeshInfo>();
    //静态表情
    [SerializeField]
    private bool _IsStatic;
    
    #region 超链接
    [System.Serializable]
    public class HrefClickEvent : UnityEvent<string,int> { }
    //点击事件监听
    public HrefClickEvent OnHrefClick = new HrefClickEvent();
    // 超链接信息列表  
    private readonly List<HrefInfo> _ListHrefInfos = new List<HrefInfo>();
    #endregion

    protected override void Start()
    {
        ActiveText();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        ActiveText();
    }
#endif

    public void ActiveText()
    {
        //支持富文本
        supportRichText = true;
        //对齐几何
        alignByGeometry = true;

        _mesh = new Mesh();
        Initialize();
        //启动的是 更新顶点
        SetVerticesDirty();
    }

    #region 初始化
    void Initialize()
    {
        if (null == m_EmojiAsset)
        {
            return;
        }

        if (null == _IndexSpriteInfo)
        {
            _IndexSpriteInfo = new Dictionary<int, Dictionary<string, SpriteInfoGroup>>();
        }

        if(!_IndexSpriteInfo.ContainsKey(m_EmojiAsset.ID))
        {
            Dictionary<string, SpriteInfoGroup> _spriteGroup = new Dictionary<string, SpriteInfoGroup>();
            foreach (var item in m_EmojiAsset.listSpriteGroup)
            {
                if (!_spriteGroup.ContainsKey(item.tag) && item.listSpriteInfo != null && item.listSpriteInfo.Count > 0)
                    _spriteGroup.Add(item.tag, item);
            }
            _IndexSpriteInfo.Add(m_EmojiAsset.ID, _spriteGroup);
        }
    }
    #endregion

    public override void SetVerticesDirty()
    {
        base.SetVerticesDirty();
        
        //设置新文本
        _OutputText = GetOutputText();
    }

    readonly UIVertex[] m_TempVerts = new UIVertex[4];
    protected override void OnPopulateMesh(VertexHelper toFill)
    {
        if (font == null)
            return;

        // We don't care if we the font Texture changes while we are doing our Update.
        // The end result of cachedTextGenerator will be valid for this instance.
        // Otherwise we can get issues like Case 619238.
        m_DisableFontTextureRebuiltCallback = true;

        Vector2 extents = rectTransform.rect.size;

        var settings = GetGenerationSettings(extents);
        //   cachedTextGenerator.PopulateWithErrors(text, settings, gameObject);
        cachedTextGenerator.Populate(_OutputText, settings);

        // Apply the offset to the vertices
        IList<UIVertex> verts = cachedTextGenerator.verts;
        float unitsPerPixel = 1 / pixelsPerUnit;
        //Last 4 verts are always a new line... (\n)
        int vertCount = verts.Count - 4;
        Vector2 roundingOffset = new Vector2(verts[0].position.x, verts[0].position.y) * unitsPerPixel;
        roundingOffset = PixelAdjustPoint(roundingOffset) - roundingOffset;
        toFill.Clear();

        ClearQuadUVs(verts);

        List<Vector3> _listVertsPos = new List<Vector3>();
        if (roundingOffset != Vector2.zero)
        {
            for (int i = 0; i < vertCount; ++i)
            {
                int tempVertsIndex = i & 3;
                m_TempVerts[tempVertsIndex] = verts[i];
                m_TempVerts[tempVertsIndex].position *= unitsPerPixel;
                m_TempVerts[tempVertsIndex].position.x += roundingOffset.x;
                m_TempVerts[tempVertsIndex].position.y += roundingOffset.y;
                if (tempVertsIndex == 3)
                    toFill.AddUIVertexQuad(m_TempVerts);
                _listVertsPos.Add(m_TempVerts[tempVertsIndex].position);
            }
        }
        else
        {
            for (int i = 0; i < vertCount; ++i)
            {
                int tempVertsIndex = i & 3;
                m_TempVerts[tempVertsIndex] = verts[i];
                m_TempVerts[tempVertsIndex].position *= unitsPerPixel;
                if (tempVertsIndex == 3)
                    toFill.AddUIVertexQuad(m_TempVerts);
                _listVertsPos.Add(m_TempVerts[tempVertsIndex].position);
             
            }
        }

        //计算quad占位的信息
        CalcQuadInfo(_listVertsPos);
        //计算包围盒
        CalcBoundsInfo(_listVertsPos, toFill, settings);

        m_DisableFontTextureRebuiltCallback = false;

    }

    #region 文本所占的长宽
    public override float preferredWidth
    {
        get
        {
            var settings = GetGenerationSettings(Vector2.zero);
            return cachedTextGeneratorForLayout.GetPreferredWidth(_OutputText, settings) / pixelsPerUnit;
        }
    }
    public override float preferredHeight
    {
        get
        {
            var settings = GetGenerationSettings(new Vector2(rectTransform.rect.size.x, 0.0f));
            return cachedTextGeneratorForLayout.GetPreferredHeight(_OutputText, settings) / pixelsPerUnit;
        }
    }
    #endregion

    #region 根据正则规则更新文本
    private string GetOutputText()
    {
        if (null == _SpriteInfo)
        {
            _SpriteInfo = new Dictionary<int, SpriteTagInfo>();
        }
        StringBuilder _textBuilder = new StringBuilder();
        int _textIndex = 0;

        var matches = _InputTagRegex.Matches(text);
        foreach (Match match in matches)
        {
            int _tempID = 0;
            if (!string.IsNullOrEmpty(match.Groups[1].Value) && !match.Groups[1].Value.Equals("-"))
            {
                _tempID = int.Parse(match.Groups[1].Value);
            }
            string _tempTag = match.Groups[2].Value;
            //更新超链接
            if (_tempID < 0)
            {
                _textBuilder.Append(text.Substring(_textIndex, match.Index - _textIndex));
                _textBuilder.Append("<color=blue>");
                int _startIndex = _textBuilder.Length * 4;

                _textBuilder.Append("[");
                _textBuilder.Append(match.Groups[2].Value);
                _textBuilder.Append("]");

                int _endIndex = _textBuilder.Length * 4 - 2;
                _textBuilder.Append("</color>");

                var hrefInfo = new HrefInfo
                {
                    id = Mathf.Abs(_tempID),
                    startIndex = _startIndex, // 超链接里的文本起始顶点索引
                    endIndex = _endIndex,
                    name = match.Groups[2].Value
                };
                _ListHrefInfos.Add(hrefInfo);
            }
            else  //更新表情
            {
                if (!_IndexSpriteInfo.ContainsKey(_tempID)
                    || !_IndexSpriteInfo[_tempID].ContainsKey(_tempTag))
                    continue;
                SpriteInfoGroup _tempGroup = _IndexSpriteInfo[_tempID][_tempTag];

                _textBuilder.Append(text.Substring(_textIndex, match.Index - _textIndex));
                int _tempIndex = _textBuilder.Length * 4;// 每个字符四个顶点，用顶点的起始来索引表情

                // 使用 quad 标记来表示表情占位符
                _textBuilder.Append(@"<quad size=");
                _textBuilder.Append(_tempGroup.size);
                _textBuilder.Append(" width=");
                _textBuilder.Append(_tempGroup.width);
                _textBuilder.Append(" />");

                if (!_SpriteInfo.ContainsKey(_tempIndex))
                {
                    SpriteTagInfo _tempSpriteTag = new SpriteTagInfo
                    {
                        _ID = _tempID,
                        _Tag = _tempTag,
                        _Size = new Vector2(_tempGroup.size * _tempGroup.width, _tempGroup.size),
                        _Pos = new Vector3[4],// 表情的四个顶点
                        _UV = _tempGroup.listSpriteInfo[0].uv// 默认显示表情第一帧
                    };
                    _SpriteInfo.Add(_tempIndex, _tempSpriteTag);
                }
            }

            _textIndex = match.Index + match.Length;
        }

        _textBuilder.Append(text.Substring(_textIndex, text.Length - _textIndex));
        return _textBuilder.ToString();
    }
    #endregion

    #region 清除乱码
    private void ClearQuadUVs(IList<UIVertex> verts)
    {
        foreach (var item in _SpriteInfo)
        {
            if ((item.Key + 4) > verts.Count)
                continue;

            int count = item.Key + 4;
            for (int i = item.Key; i < count; i++)
            {
                //清除乱码
                UIVertex tempVertex = verts[i];
                tempVertex.uv0 = Vector2.zero;
                verts[i] = tempVertex;
            }
        }
    }
#endregion

    #region 计算Quad占位信息
    void CalcQuadInfo(List<Vector3> _listVertsPos)
    {
        foreach (var item in _SpriteInfo)
        {
            if ((item.Key + 4) > _listVertsPos.Count)
                continue;

            // 计算每个表情的四个顶点信息
            int count = item.Key + 4;
            for (int i = item.Key; i < count; i++)
            {
                var pos = _listVertsPos[i];
                // TODO：这里默认中心对齐，可以做成多种选项，如：上对其、中对其、下对齐
                pos.y -= item.Value._Size.y * 0.5f;// 让表情跟文字中心对齐
                item.Value._Pos[i - item.Key] = pos;
            }
        }
        //绘制表情
        UpdateDrawnSprite();
    }
    #endregion

    #region 绘制表情
    void UpdateDrawnSprite()
    {
        if (null == _DrawSpriteInfo)
        {
            _DrawSpriteInfo = new Dictionary<int, List<SpriteTagInfo>>();
        }
        foreach (var item in _SpriteInfo)
        {
            int _id = item.Value._ID;

            //更新绘制表情的信息
            List<SpriteTagInfo> _listSpriteInfo = null;
            if (_DrawSpriteInfo.ContainsKey(_id))
            {
                _listSpriteInfo = _DrawSpriteInfo[_id];
            }
            else
            {
                _listSpriteInfo = new List<SpriteTagInfo>();
                _DrawSpriteInfo.Add(_id, _listSpriteInfo);
            }
            _listSpriteInfo.Add(item.Value);
        }

        foreach (var item in _DrawSpriteInfo)
        {
            UpdateTextInfo(item.Key, item.Value);
        }
    }
    #endregion

    void Update()
    {
        Debug.LogError("Update ------------------");
        //动态表情
        if (!_IsStatic)
            DrawSpriteAnimation();
    }

    public void UpdateTextInfo(int _id, List<SpriteTagInfo> _value)
    {
        if (null == _value || _value.Count <= 0)
            return;
        int _spriteTagCount = _value.Count;
        //Vector3 _textPos = transform.position;
        //Vector3 _spritePos = _IndexSpriteGraphic[_id]._SpriteGraphic.transform.position;
        //Vector3 _spritePos = transform.position;
        //Vector3 _disPos = (_textPos - _spritePos) * (1.0f / pixelsPerUnit);

        MeshInfo _meshInfo = new MeshInfo();
        _meshInfo._Tag = new string[_spriteTagCount];
        _meshInfo._Vertices = new Vector3[_spriteTagCount * 4];
        _meshInfo._UV = new Vector2[_spriteTagCount * 4];
        _meshInfo._Triangles = new int[_spriteTagCount * 6];
        for (int i = 0; i < _value.Count; i++)
        {
            int m = i * 4;
            //标签
            _meshInfo._Tag[i] = _value[i]._Tag;
            //顶点位置
            //_meshInfo._Vertices[m + 0] = _value[i]._Pos[0] + _disPos;
            //_meshInfo._Vertices[m + 1] = _value[i]._Pos[1] + _disPos;
            //_meshInfo._Vertices[m + 2] = _value[i]._Pos[2] + _disPos;
            //_meshInfo._Vertices[m + 3] = _value[i]._Pos[3] + _disPos;

            _meshInfo._Vertices[m + 0] = _value[i]._Pos[0];
            _meshInfo._Vertices[m + 1] = _value[i]._Pos[1];
            _meshInfo._Vertices[m + 2] = _value[i]._Pos[2];
            _meshInfo._Vertices[m + 3] = _value[i]._Pos[3];
            //uv
            _meshInfo._UV[m + 0] = _value[i]._UV[0];
            _meshInfo._UV[m + 1] = _value[i]._UV[1];
            _meshInfo._UV[m + 2] = _value[i]._UV[2];
            _meshInfo._UV[m + 3] = _value[i]._UV[3];
        }
        if (_TextMeshInfo.ContainsKey(_id))
        {
            MeshInfo _oldMeshInfo = _TextMeshInfo[_id];
            if (_meshInfo.Equals(_oldMeshInfo))
                return;
            else
                _TextMeshInfo[_id] = _meshInfo;
        }
        else
            _TextMeshInfo.Add(_id, _meshInfo);

        //更新图片
        DrawSprites(_id);
    }

    #region 播放动态表情
    float _animationTime = 0.0f;
    int _AnimationIndex = 0;
    private void DrawSpriteAnimation()
    {
        if(null == m_EmojiAsset)
        {
            return;
        }
        _animationTime += Time.deltaTime * m_EmojiSpeed;
        if (_animationTime >= 1.0f)
        {
            _AnimationIndex++;
            //绘制表情
            //foreach (var item in _IndexSpriteGraphic)
            {
                int id = m_EmojiAsset.ID;
                if (null == m_EmojiAsset || m_EmojiAsset._IsStatic || null == _TextMeshInfo || !_TextMeshInfo.ContainsKey(id))
                {
                    _animationTime = 0.0f;
                    return;
                }
                
                MeshInfo data = _TextMeshInfo[id];
                //foreach (var item02 in _data)
                {
                    for (int i = 0; i < data._Tag.Length; i++)
                    {
                        List<SpriteInfo> _listSpriteInfo = _IndexSpriteInfo[id][data._Tag[i]].listSpriteInfo;
                        if (_listSpriteInfo.Count <= 1)
                            continue;
                        int _index = _AnimationIndex % _listSpriteInfo.Count;

                        int m = i * 4;
                        data._UV[m + 0] = _listSpriteInfo[_index].uv[0];
                        data._UV[m + 1] = _listSpriteInfo[_index].uv[1];
                        data._UV[m + 2] = _listSpriteInfo[_index].uv[2];
                        data._UV[m + 3] = _listSpriteInfo[_index].uv[3];

                    }
                }
                // _IndexSpriteGraphic[item.Key]._Mesh = _mesh;
                DrawSprites(id);
            }

            _animationTime = 0.0f;
        }
    }
    #endregion

    #region 绘制图片
    private void DrawSprites(int _id)
    {
        if (!_TextMeshInfo.ContainsKey(_id))
            return;
        //SpriteGraphic _spriteGraphic = _IndexSpriteGraphic[_id]._SpriteGraphic;
        //Mesh _mesh = new Mesh();// _IndexSpriteGraphic[_id]._Mesh;
        MeshInfo _data = _TextMeshInfo[_id];
        List<Vector3> _vertices = new List<Vector3>();
        List<Vector2> _uv = new List<Vector2>();
        List<int> _triangles = new List<int>();
        ///foreach (var item in _data)
        {
            for (int i = 0; i < _data._Vertices.Length; i++)
            {
                //添加顶点
                _vertices.Add(_data._Vertices[i]);
                //添加uv
                _uv.Add(_data._UV[i]);
            }
            //添加顶点索引
            for (int i = 0; i < _data._Triangles.Length; i++)
                _triangles.Add(_data._Triangles[i]);
        }
        //计算顶点绘制顺序
        for (int i = 0; i < _triangles.Count; i++)
        {
            if (i % 6 == 0)
            {
                int num = i / 6;
                _triangles[i + 0] = 0 + 4 * num;
                _triangles[i + 1] = 1 + 4 * num;
                _triangles[i + 2] = 2 + 4 * num;

                _triangles[i + 3] = 0 + 4 * num;
                _triangles[i + 4] = 2 + 4 * num;
                _triangles[i + 5] = 3 + 4 * num;
            }
        }
        _mesh.Clear();
        _mesh.vertices = _vertices.ToArray();
        _mesh.uv = _uv.ToArray();
        _mesh.triangles = _triangles.ToArray();

        this.canvasRenderer.SetMesh(_mesh);
        UpdateMaterial();
    }
    #endregion

    #region 模型数据信息
    private class MeshInfo
    {
        public string[] _Tag;
        public Vector3[] _Vertices;
        public Vector2[] _UV;
        public int[] _Triangles;

        //比较数据是否一样
        public bool Equals(MeshInfo _value)
        {
            if (_Tag.Length != _value._Tag.Length || _Vertices.Length != _value._Vertices.Length)
                return false;
            for (int i = 0; i < _Tag.Length; i++)
                if (_Tag[i] != _value._Tag[i])
                    return false;
            for (int i = 0; i < _Vertices.Length; i++)
                if (_Vertices[i] != _value._Vertices[i])
                    return false;
            return true;
        }
    }
    #endregion






    #region 处理超链接的包围盒
    void CalcBoundsInfo(List<Vector3> _listVertsPos, VertexHelper toFill,TextGenerationSettings settings)
    {
        #region 包围框
        // 处理超链接包围框  
        foreach (var hrefInfo in _ListHrefInfos)
        {
            hrefInfo.boxes.Clear();
            if (hrefInfo.startIndex >= _listVertsPos.Count)
            {
                continue;
            }

            // 将超链接里面的文本顶点索引坐标加入到包围框  
            var pos = _listVertsPos[hrefInfo.startIndex];
            var bounds = new Bounds(pos, Vector3.zero);
            for (int i = hrefInfo.startIndex, m = hrefInfo.endIndex; i < m; i++)
            {
                if (i >= _listVertsPos.Count)
                {
                    break;
                }

                pos = _listVertsPos[i];
                if (pos.x < bounds.min.x)
                {
                    // 换行重新添加包围框  
                    hrefInfo.boxes.Add(new Rect(bounds.min, bounds.size));
                    bounds = new Bounds(pos, Vector3.zero);
                }
                else
                {
                    bounds.Encapsulate(pos); // 扩展包围框  
                }
            }
            //添加包围盒
            hrefInfo.boxes.Add(new Rect(bounds.min, bounds.size));
        }
        #endregion

        #region 添加下划线
        TextGenerator _UnderlineText = new TextGenerator();
        _UnderlineText.Populate("_", settings);
        IList<UIVertex> _TUT = _UnderlineText.verts;
        foreach (var item in _ListHrefInfos)
        {
            for (int i = 0; i < item.boxes.Count; i++)
            {
                //计算下划线的位置
                Vector3[] _ulPos = new Vector3[4];
                _ulPos[0] = item.boxes[i].position + new Vector2(0.0f, fontSize * 0.2f);
                _ulPos[1] = _ulPos[0]+new Vector3(item.boxes[i].width,0.0f);
                _ulPos[2] = item.boxes[i].position + new Vector2(item.boxes[i].width, 0.0f);
                _ulPos[3] =item.boxes[i].position;
                //绘制下划线
                for (int j = 0; j < 4; j++)
                {
                    m_TempVerts[j] = _TUT[j];
                    m_TempVerts[j].color = Color.blue;
                    m_TempVerts[j].position = _ulPos[j];
                    if (j == 3)
                        toFill.AddUIVertexQuad(m_TempVerts);
                }

            }
        }

        #endregion

    }
    #endregion
    
    #region  超链接信息类
    private class HrefInfo
    {
        public int id;

        public int startIndex;

        public int endIndex;

        public string name;

        public readonly List<Rect> boxes = new List<Rect>();
    }
    #endregion

    #region 点击事件检测是否点击到超链接文本  
    public void OnPointerClick(PointerEventData eventData)
    {
        Vector2 lp;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, eventData.position, eventData.pressEventCamera, out lp);

        foreach (var hrefInfo in _ListHrefInfos)
        {
            var boxes = hrefInfo.boxes;
            for (var i = 0; i < boxes.Count; ++i)
            {
                if (boxes[i].Contains(lp))
                {
                    OnHrefClick.Invoke(hrefInfo.name, hrefInfo.id);
                    return;
                }
            }
        }
    }
    #endregion
}

public class SpriteTagInfo
{
    //图集ID
    public int _ID;
    //标签标签
    public string _Tag;
    //标签大小
    public Vector2 _Size;
    //表情位置
    public Vector3[] _Pos;
    //uv
    public Vector2[] _UV;
}


