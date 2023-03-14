public class CPULife
{
  static int getBit(int input, int pos)
  {
    return (input >> (pos)) & 1;
  }

  static void setBit(ref int input, int pos)
  {
    input |= (1 << pos);
  }

  static void clearBit(ref int input, int pos)
  {
    input &= ~(1 << pos);
  }

  public static void CalculateLife(int[] gridIn, int[] gridOut, int gridWidth, int resolution)
  {
    for (int y = 1; y < resolution - 1; y++)
    {
      for (int x = 1; x < gridWidth - 1; x++)
      {
        int index = y * gridWidth + x;

        // Get neighbouring indexes
        int topLeft = gridIn[index - gridWidth - 1];
        int top = gridIn[index - gridWidth];
        int topRight = gridIn[index - gridWidth + 1];
        int left = gridIn[index - 1];
        int cell = gridIn[index];
        int right = gridIn[index + 1];
        int bottomLeft = gridIn[index + gridWidth - 1];
        int bottom = gridIn[index + gridWidth];
        int bottomRight = gridIn[index + gridWidth + 1];

        int output = 0;

        // Leftmost cell (Bit 0)
        int neighbours = getBit(topLeft, 31);
        neighbours += getBit(top, 0);
        neighbours += getBit(top, 1);
        neighbours += getBit(left, 31);
        neighbours += getBit(cell, 1);
        neighbours += getBit(bottomLeft, 31);
        neighbours += getBit(bottom, 0);
        neighbours += getBit(bottom, 1);

        bool alive = getBit(cell, 0) > 0;
        if ((alive && neighbours == 2) || neighbours == 3)
        {
          setBit(ref output, 0);
        }
        else
        {
          clearBit(ref output, 0);
        }

        // All middle cells (Bits 1-30)
        for (int i = 1; i < 31; i++)
        {
          neighbours = getBit(top, i - 1);
          neighbours += getBit(top, i);
          neighbours += getBit(top, i + 1);
          neighbours += getBit(cell, i - 1);
          neighbours += getBit(cell, i + 1);
          neighbours += getBit(bottom, i - 1);
          neighbours += getBit(bottom, i);
          neighbours += getBit(bottom, i + 1);

          alive = getBit(cell, i) > 0;
          if ((alive && neighbours == 2) || neighbours == 3)
          {
            setBit(ref output, i);
          }
          else
          {
            clearBit(ref output, i);
          }
        }

        // Rightmost cell (Bit 31)
        neighbours = getBit(top, 30);
        neighbours += getBit(top, 31);
        neighbours += getBit(topRight, 0);
        neighbours += getBit(cell, 30);
        neighbours += getBit(right, 0);
        neighbours += getBit(bottom, 30);
        neighbours += getBit(bottom, 31);
        neighbours += getBit(bottomRight, 0);

        alive = getBit(cell, 31) > 0;
        if ((alive && neighbours == 2) || neighbours == 3)
        {
          setBit(ref output, 31);
        }
        else
        {
          clearBit(ref output, 31);
        }

        gridOut[index] = output;
      }
    }
  }
}