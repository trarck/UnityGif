using UnityEngine;
using UnityEngine.UI;

namespace Gif
{
    [RequireComponent(typeof(RawImage))]
    public class GifUIRenderer : GifRenderer
    {
        RawImage m_RawImage;


        private void Awake()
        {
            if (!m_RawImage)
            {
                m_RawImage = GetComponent<RawImage>();
            }
        }

        protected override void Render(Texture texture)
        {
            m_RawImage.texture = texture;
            m_RawImage.SetNativeSize();
        }
    }
}