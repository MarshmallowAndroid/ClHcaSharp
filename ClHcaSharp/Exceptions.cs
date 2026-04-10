using System;

namespace ClHcaSharp
{
    public class HcaException : Exception
    {
    }

    public class HcaParamsException : HcaException
    {
    }

    public class HcaHeaderException : HcaException
    {
    }

    public class HcaChecksumException : HcaException
    {
    }

    public class HcaSyncException : HcaException
    {
    }

    public class HcaUnpackException : HcaException
    {
    }

    public class HcaBitReaderException : HcaException
    {
    }
}
