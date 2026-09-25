namespace AllNodes
{
    public class Everything<T> : System.Object, System.Object
    {
        private static System.Collections.Generic.List<T> Items
        {
            public get
            {
                // Variables
                System.Collections.Generic.List<T> varList_T_ = default(System.Collections.Generic.List<T>);
                // Return
                // Get Everything<T>.Items
                varList_T_ = AllNodes.Everything<T>.Items;
                return varList_T_;
            }

            public set
            {
                // Variables
                System.Collections.Generic.List<T> varList_T_ = default(System.Collections.Generic.List<T>);
                // Set Everything<T>.Items
                AllNodes.Everything<T>.Items = value;
                varList_T_ = value;
            // Return
            }
        }

        // Everything
        public Everything()
        {
        // Variables
        }

        // Main
        public static System.Tuple<System.Int32, System.String> Main<T0>(System.Int32 varfirst, System.String varInput1)
        {
            System.Collections.Generic.Stack<int> jumpStack = new System.Collections.Generic.Stack<int>();
            // Variables
            System.Int32 varIndex = default(System.Int32);
            System.Exception varException = default(System.Exception);
            // If Else
            if (true)
            {
            }
            else
            {
                goto State4;
            }

            // For Loop
            varIndex = ;
            if (varIndex < 10)
            {
                jumpStack.Push(2);
                goto State5;
            }

            State2:
                // For Loop
                varIndex++;
            if (varIndex < 10)
            {
                jumpStack.Push(2);
                goto State5;
            }

            // Return
            return new System.Tuple<System.Int32, System.String>(0, "");
            State4:
                // Throw Exception
                throw varException;
            // Jump stack
            State5:
                if (jumpStack.Count == 0)
                    throw new System.Exception();
            switch (jumpStack.Pop())
            {
                case 2:
                    goto State2;
                default:
                    throw new System.Exception();
            }
        }
    }
}