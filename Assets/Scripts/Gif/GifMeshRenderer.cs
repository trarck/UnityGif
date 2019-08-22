using UnityEngine;

namespace Gif
{
    [RequireComponent(typeof(MeshRenderer))]
    public class GifMeshRenderer : GifRenderer
    {
       
        [SerializeField]
        string m_TextureName = "_MainTex";
        MeshRenderer m_MeshRenderer;
        int m_TextureId;


        private void Awake()
        {
            if (!m_MeshRenderer)
            {
                m_MeshRenderer = GetComponent<MeshRenderer>();
            }
        }

        protected override void Render(Texture texture)
        {
            m_MeshRenderer.sharedMaterial.SetTexture(m_TextureId, texture);
        }

        protected override void AfterCreateTextures()
        {
            base.AfterCreateTextures();
            m_TextureId = Shader.PropertyToID(m_TextureName);
        }
    }
}