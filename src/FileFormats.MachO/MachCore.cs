﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileFormats.MachO
{
    public class MachCore
    {
        IAddressSpace _dataSource;
        MachOFile _machO;
        ulong _dylinkerHintAddress;
        Lazy<ulong> _dylinkerAddress;
        Lazy<MachDyld> _dylinker;
        Lazy<MachLoadedImage[]> _loadedImages;

        public MachCore(IAddressSpace dataSource, ulong dylinkerHintAddress = 0)
        {
            _dataSource = dataSource;
            _machO = new MachOFile(dataSource);
            _dylinkerHintAddress = dylinkerHintAddress;
            _dylinkerAddress = new Lazy<ulong>(FindDylinker);
            _dylinker = new Lazy<MachDyld>(() => new MachDyld(new MachOFile(VirtualAddressReader.DataSource, DylinkerAddress, true)));
            _loadedImages = new Lazy<MachLoadedImage[]>(ReadImages);
        }

        public bool IsValidCoreFile { get { return _machO.HeaderMagic.IsMagicValid.Check(); } }
        public Reader VirtualAddressReader {  get { return _machO.VirtualAddressReader; } }
        public ulong DylinkerAddress { get { return _dylinkerAddress.Value; } }
        public MachDyld Dylinker { get { return _dylinker.Value; } }
        public IEnumerable<MachLoadedImage> LoadedImages { get { return _loadedImages.Value; } }

        ulong FindDylinker()
        {
            if(_dylinkerHintAddress != 0 && IsValidDylinkerAddress(_dylinkerHintAddress))
            {
                return _dylinkerHintAddress;
            }
            foreach (MachSegment segment in _machO.Segments)
            {
                ulong position = segment.LoadCommand.VMAddress;
                for (ulong offset = 0; offset < segment.LoadCommand.FileSize; offset += 0x1000)
                {
                    if (IsValidDylinkerAddress(position + offset))
                    {
                        return position + offset;
                    }
                }
            }
            throw new BadInputFormatException("No dylinker module found");
        }

        bool IsValidDylinkerAddress(ulong possibleDylinkerAddress)
        {
            MachOFile dylinker = new MachOFile(VirtualAddressReader.DataSource, possibleDylinkerAddress, true);
            return dylinker.HeaderMagic.IsMagicValid.Check() && 
                   dylinker.Header.FileType == MachHeaderFileType.Dylinker;
        }

        MachLoadedImage[] ReadImages()
        {
            return Dylinker.Images.Select(i => new MachLoadedImage(new MachOFile(VirtualAddressReader.DataSource, i.LoadAddress, true), i)).ToArray();
        }
    }

    public class MachLoadedImage
    {
        DyldLoadedImage _dyldLoadedImage;

        public MachLoadedImage(MachOFile image, DyldLoadedImage dyldLoadedImage)
        {
            Image = image;
            _dyldLoadedImage = dyldLoadedImage;
        }

        public MachOFile Image { get; private set; }
        public ulong LoadAddress {  get { return _dyldLoadedImage.LoadAddress; } }
        public string Path { get { return _dyldLoadedImage.Path; } }
    }

    public class MachDyld
    {
        MachOFile _dyldImage;
        Lazy<ulong> _dyldAllImageInfosAddress;
        Lazy<DyldImageAllInfosV2> _dyldAllImageInfos;
        Lazy<DyldImageInfo[]> _imageInfos;
        Lazy<DyldLoadedImage[]> _images;

        public MachDyld(MachOFile dyldImage)
        {
            _dyldImage = dyldImage;
            _dyldAllImageInfosAddress = new Lazy<ulong>(FindAllImageInfosAddress);
            _dyldAllImageInfos = new Lazy<DyldImageAllInfosV2>(ReadAllImageInfos);
            _imageInfos = new Lazy<DyldImageInfo[]>(ReadImageInfos);
            _images = new Lazy<DyldLoadedImage[]>(ReadLoadedImages);
        }

        public ulong AllImageInfosAddress {  get { return _dyldAllImageInfosAddress.Value; } }
        public DyldImageAllInfosV2 AllImageInfos { get { return _dyldAllImageInfos.Value; } }
        public IEnumerable<DyldImageInfo> ImageInfos { get { return _imageInfos.Value; } }
        public IEnumerable<DyldLoadedImage> Images {  get { return _images.Value; } }

        ulong FindAllImageInfosAddress()
        {
            ulong preferredAddress = _dyldImage.Symtab.Symbols.Where(s => s.Name == "_dyld_all_image_infos").First().Value;
            return preferredAddress - _dyldImage.PreferredVMBaseAddress + _dyldImage.LoadAddress;
        }
        
        DyldImageAllInfosV2 ReadAllImageInfos()
        {
            DyldImageAllInfosVersion version = _dyldImage.VirtualAddressReader.Read<DyldImageAllInfosVersion>(AllImageInfosAddress);
            return _dyldImage.VirtualAddressReader.Read<DyldImageAllInfosV2>(AllImageInfosAddress);
        }

        DyldImageInfo[] ReadImageInfos()
        {
            return _dyldImage.VirtualAddressReader.ReadArray<DyldImageInfo>(AllImageInfos.InfoArray, AllImageInfos.InfoArrayCount);
        }

        DyldLoadedImage[] ReadLoadedImages()
        {
            return ImageInfos.Select(i => new DyldLoadedImage(_dyldImage.VirtualAddressReader.Read<string>(i.PathAddress), i )).ToArray();
        }
    }

    public class DyldLoadedImage
    {
        DyldImageInfo _imageInfo;

        public DyldLoadedImage(string path, DyldImageInfo imageInfo)
        {
            Path = path;
            _imageInfo = imageInfo;
        }

        public string Path;
        public ulong LoadAddress { get { return _imageInfo.Address; } }
    }
}
