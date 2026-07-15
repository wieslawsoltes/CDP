using System;

namespace CdpGalleryApp.ViewModels;

public class ChartsPageViewModel : ViewModelBase
{
    private double _cpuScripting = 30.0;
    private double _cpuRendering = 20.0;
    private double _cpuLayout = 15.0;
    private double _cpuSystem = 10.0;
    private double _cpuIdle = 25.0;

    private long _gen0Size = 50 * 1024 * 1024; // 50 MB
    private long _gen1Size = 30 * 1024 * 1024; // 30 MB
    private long _gen2Size = 100 * 1024 * 1024; // 100 MB
    private long _lohSize = 40 * 1024 * 1024; // 40 MB

    public double CpuScripting
    {
        get => _cpuScripting;
        set => RaiseAndSetIfChanged(ref _cpuScripting, value);
    }

    public double CpuRendering
    {
        get => _cpuRendering;
        set => RaiseAndSetIfChanged(ref _cpuRendering, value);
    }

    public double CpuLayout
    {
        get => _cpuLayout;
        set => RaiseAndSetIfChanged(ref _cpuLayout, value);
    }

    public double CpuSystem
    {
        get => _cpuSystem;
        set => RaiseAndSetIfChanged(ref _cpuSystem, value);
    }

    public double CpuIdle
    {
        get => _cpuIdle;
        set => RaiseAndSetIfChanged(ref _cpuIdle, value);
    }

    public long Gen0Size
    {
        get => _gen0Size;
        set
        {
            if (RaiseAndSetIfChanged(ref _gen0Size, value))
            {
                OnPropertyChanged(nameof(Gen0SizeMb));
            }
        }
    }

    public long Gen1Size
    {
        get => _gen1Size;
        set
        {
            if (RaiseAndSetIfChanged(ref _gen1Size, value))
            {
                OnPropertyChanged(nameof(Gen1SizeMb));
            }
        }
    }

    public long Gen2Size
    {
        get => _gen2Size;
        set
        {
            if (RaiseAndSetIfChanged(ref _gen2Size, value))
            {
                OnPropertyChanged(nameof(Gen2SizeMb));
            }
        }
    }

    public long LohSize
    {
        get => _lohSize;
        set
        {
            if (RaiseAndSetIfChanged(ref _lohSize, value))
            {
                OnPropertyChanged(nameof(LohSizeMb));
            }
        }
    }

    // Helper properties for sliders (bound in Megabytes)
    public double Gen0SizeMb
    {
        get => _gen0Size / (1024.0 * 1024.0);
        set => Gen0Size = (long)(value * 1024.0 * 1024.0);
    }

    public double Gen1SizeMb
    {
        get => _gen1Size / (1024.0 * 1024.0);
        set => Gen1Size = (long)(value * 1024.0 * 1024.0);
    }

    public double Gen2SizeMb
    {
        get => _gen2Size / (1024.0 * 1024.0);
        set => Gen2Size = (long)(value * 1024.0 * 1024.0);
    }

    public double LohSizeMb
    {
        get => _lohSize / (1024.0 * 1024.0);
        set => LohSize = (long)(value * 1024.0 * 1024.0);
    }
}
