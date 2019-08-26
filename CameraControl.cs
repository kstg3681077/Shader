using UnityEngine;
using System.Collections;

public enum ENUM_CAMACTION
{
    None,
    Auto,
    LeftMove,
    RightMove,
    FowardMove,
    BackMove,
    LeftRot,
    RightRot,
};

public class CameraControl : MonoBehaviour
{
    const float c_fSmooth = 0.2f;

    float m_fScrollWheel;
    float m_fHorizontal;
    float m_fVertical;

    Transform m_pOffset;

    Vector3 m_TargetPos;

    Vector3 m_ScrollTarget;

    ENUM_CAMACTION m_CamAction = ENUM_CAMACTION.None;

    /// <summary>  
    /// 辅助摄像机  
    /// </summary>  
    public Camera m_pSelectCamera;

    public Camera m_pAlarmCamera;

    #region 纯色材质 SelectColorMaterail  
    public Shader SelectColorShader;
    private Material m_Select = null;
    private Material SelectMaterail
    {
        get
        {
            if (m_Select == null)
            {
                m_Select = new Material(SelectColorShader);
            }
            return m_Select;
        }
    }
    #endregion

    #region 纯色材质 AlarmColorMaterail  
    public Shader m_pAlarmColorShader;
    private Material m_pAlarm = null;
    private Material AlarmMaterail
    {
        get
        {
            if (m_pAlarm == null)
            {
                m_pAlarm = new Material(m_pAlarmColorShader);
            }
            return m_pAlarm;
        }
    }
    #endregion

    #region 合并材质 CompositeMaterial  
    public Shader m_pCompositeShader;
    private Material m_pComposite = null;
    private Material CompositeMaterial
    {
        get
        {
            if (m_pComposite == null)
                m_pComposite = new Material(m_pCompositeShader);
            return m_pComposite;
        }
    }
    #endregion

    #region 模糊材质 BlurMaterial  
    public Shader m_pBlurShader;
    private Material m_pBlur = null;
    private Material BlurMaterial
    {
        get
        {
            if (m_pBlur == null)
                m_pBlur = new Material(m_pBlurShader);
            return m_pBlur;
        }
    }
    #endregion

    #region 剔除材质 CutoffShader  
    public Shader m_pCutoffShader;
    private Material m_pCutoff = null;
    private Material CutoffMaterial
    {
        get
        {
            if (m_pCutoff == null)
                m_pCutoff = new Material(m_pCutoffShader);
            return m_pCutoff;
        }
    }
    #endregion

    /// <summary>  
    /// 辅助摄像机渲染的RenderTexture  
    /// </summary>  
    private RenderTexture m_pOutlineRenderTex;

    private RenderTexture m_pSelectTex;
    private RenderTexture m_pAlarmTex;
    /// <summary>  
    /// 模糊扩大次数  
    /// </summary>  
    public int m_nIterations = 2;

    private void Awake()
    {
        m_pOffset = transform.parent;

        m_fVertical = m_pOffset.eulerAngles.x;
        m_fHorizontal = m_pOffset.eulerAngles.y;

        m_ScrollTarget = transform.localPosition;

        m_TargetPos = m_pOffset.position;

        UIController.Instance.SetCamAction(SetCamAction);
    }

    void LateUpdate()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != AllStrings.SingleScene)
        {
            //镜头移动
            if (Vector3.Distance(m_pOffset.position, m_TargetPos) > c_fSmooth)
            {
                m_pOffset.position = Vector3.Lerp(m_pOffset.position, m_TargetPos, c_fSmooth);
            }
            else
            {
                m_pOffset.position = m_TargetPos;
            }

            Drag();

            Scroll();

            Rotate();

            Action();
        }
    }

    void OnPreRender()
    {
        m_pOutlineRenderTex = RenderTexture.GetTemporary(Screen.width, Screen.height, 16);
        m_pSelectTex = RenderTexture.GetTemporary(Screen.width, Screen.height, 16);
        m_pAlarmTex = RenderTexture.GetTemporary(Screen.width, Screen.height, 16);


        m_pAlarmCamera.targetTexture = m_pAlarmTex;
        m_pAlarmCamera.RenderWithShader(AlarmMaterail.shader, "");

        m_pSelectCamera.targetTexture = m_pSelectTex;
        m_pSelectCamera.RenderWithShader(SelectMaterail.shader, "");

        RenderTexture _renderTextureAlarm = RenderTexture.GetTemporary(m_pOutlineRenderTex.width, m_pOutlineRenderTex.height, 0);
        RenderTexture _renderTextureSelect = RenderTexture.GetTemporary(m_pOutlineRenderTex.width, m_pOutlineRenderTex.height, 0);

        //放大
        MixRender(m_pAlarmTex, ref _renderTextureAlarm);
        MixRender(m_pSelectTex, ref _renderTextureSelect);

        //_renderTextureAlarm覆盖outlineRenderTex
        Graphics.Blit(_renderTextureAlarm, m_pOutlineRenderTex);
        //从outlineRenderTex中抠掉_renderTextureSelect
        Graphics.Blit(_renderTextureSelect, m_pOutlineRenderTex, CutoffMaterial);
        //从outlineRenderTex中抠掉AlarmTex
        Graphics.Blit(m_pAlarmTex, m_pOutlineRenderTex, CutoffMaterial);
        //从_renderTextureSelect中抠掉SelectTex
        Graphics.Blit(m_pSelectTex, _renderTextureSelect, CutoffMaterial);
        //以_renderTextureSelect的Alpha融合进outlineRenderTex
        Graphics.Blit(_renderTextureSelect, m_pOutlineRenderTex, CompositeMaterial);

        RenderTexture.ReleaseTemporary(_renderTextureAlarm);
        RenderTexture.ReleaseTemporary(_renderTextureSelect);


        m_pAlarmCamera.targetTexture = null;
        m_pSelectCamera.targetTexture = null;

        RenderTexture.ReleaseTemporary(m_pOutlineRenderTex);
        RenderTexture.ReleaseTemporary(m_pSelectTex);
        RenderTexture.ReleaseTemporary(m_pAlarmTex);
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(source, destination);

        Graphics.Blit(m_pOutlineRenderTex, destination, CompositeMaterial);
    }

    void MixRender(RenderTexture in_outerTexture, ref RenderTexture _renderTexture)
    {
        RenderTexture buffer = RenderTexture.GetTemporary(in_outerTexture.width, in_outerTexture.height, 0);
        RenderTexture buffer2 = RenderTexture.GetTemporary(in_outerTexture.width, in_outerTexture.height, 0);

        Graphics.Blit(in_outerTexture, buffer);

        //多次模糊放大  
        for (int i = 0; i < m_nIterations; i++)
        {
            FourTapCone(buffer, buffer2, i);
            Graphics.Blit(buffer2, buffer);
        }
        Graphics.Blit(buffer, _renderTexture);

        RenderTexture.ReleaseTemporary(buffer);
        RenderTexture.ReleaseTemporary(buffer2);
    }

    float Spread = 0.8f;
    void FourTapCone(RenderTexture source, RenderTexture dest, int iteration)
    {
        float off = 0.5f + iteration * Spread;
        Graphics.BlitMultiTap(source, dest, BlurMaterial,
                               new Vector2(off, off),
                               new Vector2(-off, off),
                               new Vector2(off, -off),
                               new Vector2(-off, -off)
                               );
    }

    /// <summary>
    /// 滚轮切换
    /// </summary>
    void Scroll()
    {
        m_fScrollWheel = Input.GetAxis(AllStrings.ScrollWheel);
        //  var ddd = Input.inputString;
        //  if (!string.IsNullOrEmpty(ddd))
        //  {
        //      Debug.Log(KeyCode.UpArrow.ToString()+"--------------------"+ddd);
        //      if (ddd == KeyCode.P.ToString().ToLower())
        //      {
        //          Debug.Log(ddd);
        //      }        
        //  }
        if (Input.GetKeyDown(KeyCode.PageUp))
        {
            m_fScrollWheel = 1;
        }

        if (Input.GetKeyDown(KeyCode.PageDown))
        {
            m_fScrollWheel = -1;
        }

        if (m_fScrollWheel != 0)
        {
            if (m_fScrollWheel > 0 && (Vector3.zero - m_ScrollTarget).magnitude > 0f)
            {
                m_ScrollTarget = 2f * m_ScrollTarget / 3f;
            }
            else if (m_fScrollWheel < 0 && (Vector3.zero - m_ScrollTarget).magnitude <= 1000f)
            {
                m_ScrollTarget = 3f / 2f * m_ScrollTarget;
            }
        }

        if (Vector3.Distance(transform.localPosition, m_ScrollTarget) > c_fSmooth)
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, m_ScrollTarget, c_fSmooth);
        }
        else
        {
            transform.localPosition = m_ScrollTarget;
        }

    }

    /// <summary>
    /// 镜头旋转
    /// </summary>
    void Rotate()
    {
        if (Input.GetMouseButton(1))
        {
            m_fHorizontal += Input.GetAxis(AllStrings.MouseX);

            m_fVertical += Input.GetAxis(AllStrings.MouseY);
        }

        if (Input.GetKey(KeyCode.A))
        {
            m_fHorizontal += 1f;
        }

        if (Input.GetKey(KeyCode.D))
        {
            m_fHorizontal -= 1f;
        }

        if (Input.GetKey(KeyCode.W))
        {
            m_fVertical += 1f;
        }

        if (Input.GetKey(KeyCode.S))
        {
            m_fVertical -= 1f;
        }

        m_fVertical = Mathf.Clamp(m_fVertical, 5f, 80f);

        m_pOffset.rotation = Quaternion.Euler(new Vector3(m_fVertical, m_fHorizontal, 0));
    }

    void SetCamAction(ENUM_CAMACTION action)
    {
        m_CamAction = action;
    }

    void Action()
    {
        if (m_CamAction == ENUM_CAMACTION.Auto)
        {
            m_fHorizontal -= 1.5f * Time.deltaTime;

            m_pOffset.rotation = Quaternion.Euler(new Vector3(m_fVertical, m_fHorizontal, 0));
        }
        else if (m_CamAction == ENUM_CAMACTION.LeftMove)
        {
            Vector3 pos = m_pOffset.rotation * new Vector3(-1f, 0, 0);

            m_TargetPos += pos;
        }
        else if (m_CamAction == ENUM_CAMACTION.RightMove)
        {
            Vector3 pos = m_pOffset.rotation * new Vector3(1f, 0, 0);

            m_TargetPos += pos;
        }
        else if (m_CamAction == ENUM_CAMACTION.FowardMove)
        {
            Vector3 pos = m_pOffset.rotation * new Vector3(0, 1f, 1f);

            m_TargetPos += pos;

            m_TargetPos = Clamp(m_TargetPos, new Vector3(m_TargetPos.x, -2f, m_TargetPos.z), new Vector3(m_TargetPos.x, 5f, m_TargetPos.z));
        }
        else if (m_CamAction == ENUM_CAMACTION.BackMove)
        {
            Vector3 pos = m_pOffset.rotation * new Vector3(0, -1f, -1f);

            m_TargetPos += pos;

            m_TargetPos = Clamp(m_TargetPos, new Vector3(m_TargetPos.x, -2f, m_TargetPos.z), new Vector3(m_TargetPos.x, 5f, m_TargetPos.z));
        }
        else if (m_CamAction == ENUM_CAMACTION.LeftRot)
        {
            m_fHorizontal += 1f;

            m_pOffset.rotation = Quaternion.Euler(new Vector3(m_fVertical, m_fHorizontal, 0));
        }
        else if (m_CamAction == ENUM_CAMACTION.RightRot)
        {
            m_fHorizontal -= 1f;

            m_pOffset.rotation = Quaternion.Euler(new Vector3(m_fVertical, m_fHorizontal, 0));
        }
    }

    /// <summary>
    /// 镜头拖动
    /// </summary>
    void Drag()
    {
        Vector3 Pos;

        if (Input.GetMouseButton(2))
        {
            Pos = m_pOffset.rotation * new Vector3(-Input.GetAxis(AllStrings.MouseX), Input.GetAxis(AllStrings.MouseY), Input.GetAxis(AllStrings.MouseY));

            m_TargetPos += Pos;
        }

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            Pos = m_pOffset.rotation * new Vector3(-1f, 0f, 0f);

            m_TargetPos += Pos;
        }

        if (Input.GetKey(KeyCode.RightArrow))
        {
            Pos = m_pOffset.rotation * new Vector3(1f, 0f, 0f);

            m_TargetPos += Pos;
        }

        if (Input.GetKey(KeyCode.UpArrow))
        {
            Pos = m_pOffset.rotation * new Vector3(0f, 1f, 1f);

            m_TargetPos += Pos;
        }

        if (Input.GetKey(KeyCode.DownArrow))
        {
            Pos = m_pOffset.rotation * new Vector3(0f, -1f, -1f);

            m_TargetPos += Pos;
        }

        m_TargetPos = Clamp(m_TargetPos, new Vector3(m_TargetPos.x, -2f, m_TargetPos.z), new Vector3(m_TargetPos.x, 5f, m_TargetPos.z));
    }

    Vector3 Clamp(Vector3 Src, Vector3 Min, Vector3 Max)
    {
        Vector3 ret;
        float fX;
        float fY;
        float fZ;

        fX = Mathf.Clamp(Src.x, Min.x, Max.x);
        fY = Mathf.Clamp(Src.y, Min.y, Max.y);
        fZ = Mathf.Clamp(Src.z, Min.z, Max.z);

        ret = new Vector3(fX, fY, fZ);
        return ret;
    }

    public void SetTarget(Vector3 target, float distance)
    {
        m_TargetPos = target;

        m_ScrollTarget = m_ScrollTarget.normalized * distance;
    }

    public void SetTargetPos(float distance)
    {
        m_ScrollTarget = m_ScrollTarget.normalized * distance;
    }

    public void CameraViewStart()
    {
        transform.localPosition = new Vector3(0, 0, -1000f);
    }
}
