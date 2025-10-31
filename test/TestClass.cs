using System;

namespace TestNamespace
{
    /// <summary>
    /// This is a test class for LSP demonstration
    /// </summary>
    public class TestClass
    {
        /// <summary>
        /// Gets or sets the name property
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// A test method that adds two numbers
        /// </summary>
        /// <param name="a">First number</param>
        /// <param name="b">Second number</param>
        /// <returns>Sum of a and b</returns>
        public int Add(int a, int b)
        {
            return a + b;
        }

        public void PrintMessage(string message)
        {
            Console.WriteLine(message);
        }
    }
}
