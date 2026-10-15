namespace NetPrints.Editor.Dialogs;

/// <summary>What the user chose when asked where a sample is copied to.</summary>
public enum SampleTargetChoice
{
    /// <summary>Copy nothing. The default, so closing the dialog any other way cancels.</summary>
    Cancel,

    /// <summary>Copy the sample to the folder shown and open it.</summary>
    Open,

    /// <summary>Pick another parent folder first.</summary>
    Change,
}
