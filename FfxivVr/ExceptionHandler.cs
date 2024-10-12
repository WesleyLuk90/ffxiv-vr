using System;
using System.Collections.Generic;

namespace FfxivVR;
internal class ExceptionHandler
{
    private Logger logger;

    public ExceptionHandler(Logger logger)
    {
        this.logger = logger;
    }

    private Dictionary<string, int> exceptionCount = new Dictionary<string, int>();
    public void FaultBarrier(Action block)
    {
        try
        {
            block();
        }
        catch (Exception ex)
        {
            var currentCount = exceptionCount.GetValueOrDefault(ex.Message) + 1;
            exceptionCount[ex.Message] = currentCount;
            if (currentCount == 5)
            {
                logger.Error($"Got same error 5 times ({ex.Message}), surpressing");
            }
            else if (currentCount < 5)
            {
                logger.Error($"Error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
