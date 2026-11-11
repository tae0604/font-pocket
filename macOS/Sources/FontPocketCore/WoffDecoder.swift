import Foundation
import zlib

public enum WoffDecoder {
    public static func decode(_ data: Data) throws -> Data {
        func n(_ p:Int,_ count:Int) throws -> Int { guard p>=0,p+count<=data.count else{throw PocketError("Invalid WOFF table range")};return data[p..<p+count].reduce(0){($0<<8)|Int($1)} }
        guard try n(0,4)==0x774f4646,try n(8,4)==data.count else{throw PocketError("Invalid WOFF header")}
        let count=try n(12,2),size=try n(16,4)
        guard count>0,count<=4096,size>=12+count*16,size<=150*1024*1024 else{throw PocketError("WOFF size limit exceeded")}
        var output=Data(repeating:0,count:size)
        func put(_ value:Int,_ at:Int,_ bytes:Int=4){for i in 0..<bytes{output[at+i]=UInt8(truncatingIfNeeded:value>>((bytes-i-1)*8))}}
        put(try n(4,4),0);put(count,4,2)
        var power=1,selector=0;while power*2<=count{power*=2;selector+=1}
        put(power*16,6,2);put(selector,8,2);put(count*16-power*16,10,2)
        var target=12+count*16
        for i in 0..<count{
            let p=44+i*20,tag=try n(p,4),start=try n(p+4,4),compressed=try n(p+8,4),length=try n(p+12,4)
            guard start>=44+count*20,start<=data.count,compressed<=data.count-start,compressed<=length,length<=size-target else{throw PocketError("Invalid WOFF table")}
            var table=Data(repeating:0,count:length)
            if compressed==length{table=data.subdata(in:start..<start+length)}else{
                var actual=uLongf(length)
                let status=table.withUnsafeMutableBytes{dest in data.withUnsafeBytes{src in uncompress(dest.bindMemory(to:Bytef.self).baseAddress,&actual,src.bindMemory(to:Bytef.self).baseAddress!.advanced(by:start),uLong(compressed))}}
                guard status==Z_OK,actual==uLongf(length) else{throw PocketError("Damaged WOFF compression")}
            }
            put(tag,12+i*16);put(try n(p+16,4),16+i*16);put(target,20+i*16);put(length,24+i*16)
            output.replaceSubrange(target..<target+length,with:table);target += (length+3) & ~3
        }
        guard target==size else{throw PocketError("WOFF size mismatch")};return output
    }
}
