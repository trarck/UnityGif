using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Gif.Decode
{
    /// <summary>
    /// Specifies, what to do with the last image 
    /// in an animation sequence.
    /// </summary>
    public enum DisposalMethod : int
    {
        /// <summary>
        /// No disposal specified. The decoder is not 
        /// required to take any action. 
        /// </summary>
        Unspecified = 0,
        /// <summary>
        /// Do not dispose. The graphic is to be left in place. 
        /// </summary>
        NotDispose = 1,
        /// <summary>
        /// Restore to background color. 
        /// The area used by the graphic must be restored to
        /// the background color. 
        /// </summary>
        RestoreToBackground = 2,
        /// <summary>
        /// Restore to previous. The decoder is required to 
        /// restore the area overwritten by the 
        /// graphic with what was there prior to rendering the graphic. 
        /// </summary>
        RestoreToPrevious = 3
    }

    public sealed class GifImageDescriptor
    {
        /// <summary>
        /// Column number, in pixels, of the left edge of the image, 
        /// with respect to the left edge of the Logical Screen. 
        /// Leftmost column of the Logical Screen is 0.
        /// </summary>
        public short Left;
        /// <summary>
        /// Row number, in pixels, of the top edge of the image with 
        /// respect to the top edge of the Logical Screen. 
        /// Top row of the Logical Screen is 0.
        /// </summary>
        public short Top;
        /// <summary>
        /// Width of the image in pixels.
        /// </summary>
        public short Width;
        /// <summary>
        /// Height of the image in pixels.
        /// </summary>
        public short Height;
        /// <summary>
        /// Indicates the presence of a Local Color Table immediately 
        /// following this Image Descriptor.
        /// </summary>
        public bool LocalColorTableFlag;
        /// <summary>
        /// If the Local Color Table Flag is set to 1, the value in this field 
        /// is used to calculate the number of bytes contained in the Local Color Table.
        /// </summary>
        public int LocalColorTableSize;
        /// <summary>
        /// Indicates if the image is interlaced. An image is interlaced 
        /// in a four-pass interlace pattern.
        /// </summary>
        public bool InterlaceFlag;
    }

    public sealed class GifLogicalScreenDescriptor
    {
        /// <summary>
        /// Width, in pixels, of the Logical Screen where the images will 
        /// be rendered in the displaying device.
        /// </summary>
        public short Width;
        /// <summary>
        /// Height, in pixels, of the Logical Screen where the images will be 
        /// rendered in the displaying device.
        /// </summary>
        public short Height;
        /// <summary>
        /// Index into the Global Color Table for the Background Color. 
        /// The Background Color is the color used for those 
        /// pixels on the screen that are not covered by an image.
        /// </summary>
        public byte Background;
        /// <summary>
        /// Flag indicating the presence of a Global Color Table; 
        /// if the flag is set, the Global Color Table will immediately 
        /// follow the Logical Screen Descriptor.
        /// </summary>
        public bool GlobalColorTableFlag;
        /// <summary>
        /// If the Global Color Table Flag is set to 1, 
        /// the value in this field is used to calculate the number of 
        /// bytes contained in the Global Color Table.
        /// </summary>
        public int GlobalColorTableSize;
    }
    
    public sealed class GifGraphicsControlExtension
    {
        /// <summary>
        /// Indicates the way in which the graphic is to be treated after being displayed. 
        /// </summary>
        public DisposalMethod DisposalMethod;
        /// <summary>
        /// Indicates whether a transparency index is given in the Transparent Index field. 
        /// (This field is the least significant bit of the byte.) 
        /// </summary>
        public bool TransparencyFlag;
        /// <summary>
        /// The Transparency Index is such that when encountered, the corresponding pixel 
        /// of the display device is not modified and processing goes on to the next pixel.
        /// </summary>
        public int TransparencyIndex;
        /// <summary>
        /// If not 0, this field specifies the number of hundredths (1/100) of a second to 
        /// wait before continuing with the processing of the Data Stream. 
        /// The clock starts ticking immediately after the graphic is rendered. 
        /// This field may be used in conjunction with the User Input Flag field. 
        /// </summary>
        public int DelayTime;
    }
    
}