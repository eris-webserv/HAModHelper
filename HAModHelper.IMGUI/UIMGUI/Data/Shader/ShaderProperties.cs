using System;

namespace UImGui
{
	[Serializable]
	internal class ShaderProperties
	{
		public string Texture;
		public string Vertices;
		public string BaseVertex;
		public string ScreenSize;
		public string ClipRect;
		public string ClipRectKeyword;

		public ShaderProperties Clone()
		{
			return (ShaderProperties)MemberwiseClone();
		}
	}
}
