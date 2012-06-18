using System;

namespace BodyPaint
{
#if WINDOWS || XBOX
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            using (BodyPaintGame game = new BodyPaintGame())
            {
                game.Run();
            }
        }
    }
#endif
}

