public class Locals : System.Object
{
    // Main
    public static void Main()
    {
        // Variables
        System.Int32 count = default(System.Int32);
        System.Boolean varBoolean = default(System.Boolean);
        System.Int32 varInt32 = default(System.Int32);
        System.Int32 varInt322 = default(System.Int32);
        System.Int32 varInt323 = default(System.Int32);
        System.Int32 varInt324 = default(System.Int32);
        System.Int32 varInt325 = default(System.Int32);
        State0:
            // Operator Less than
            // Get count
            varInt325 = count;
        varBoolean = varInt325 < 5;
        // If Else
        if (varBoolean)
        {
        }
        else
        {
            goto State4;
        }

        // Operator Add
        // Get count
        varInt323 = count;
        varInt32 = varInt323 + 1;
        // Set count
        count = varInt32;
        varInt322 = varInt32;
        goto State0;
        State4:
            // Console.WriteLine
            // Get count
            varInt324 = count;
        System.Console.WriteLine(varInt324);
    // Return
    }
}