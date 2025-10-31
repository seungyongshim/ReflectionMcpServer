using System;
using System.Linq;
using System.Text;

namespace TestApp
{
    /// <summary>
    /// A sample calculator class for testing
    /// </summary>
    public class Calculator
    {
        /// <summary>
        /// Adds two numbers together
        /// </summary>
        /// <param name="a">First number</param>
        /// <param name="b">Second number</param>
        /// <returns>Sum of a and b</returns>
        public int Add(int a, int b)
        {
            return a + b;
        }

        /// <summary>
        /// Multiplies two numbers
        /// </summary>
        public int Multiply(int x, int y) => x * y;

        /// <summary>
        /// Gets or sets the last result
        /// </summary>
        public double LastResult { get; set; }

        private string _name = "Calculator";
    }

    public interface IOperation
    {
        double Execute(double value);
    }
}
