package main

import (
	"encoding/binary"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"time"

	"bot/proto/swoq"

	"google.golang.org/protobuf/proto"
)

type ReplayFile struct {
	stream *os.File
}

func NewReplayFile(userName string, replaysFolder string, request *swoq.StartRequest, response *swoq.StartResponse) (*ReplayFile, error) {
	// Determine file name
	dateTimeStr := time.Now().Format("20060102-150405")
	folder, err := filepath.Abs(filepath.Join(replaysFolder))
	if err != nil {
		return nil, err
	}
	filename := filepath.Join(folder, fmt.Sprintf("%s - %s - %s.swoq", userName, dateTimeStr, *response.GameId))
	// Create directory first
	if err := os.MkdirAll(filepath.Dir(filename), 0755); err != nil {
		return nil, err
	}
	// Create a new file, allow reading
	stream, err := os.OpenFile(filename, os.O_CREATE|os.O_EXCL|os.O_WRONLY, 0644)
	if err != nil {
		return nil, err
	}

	// Store header
	header := &swoq.ReplayHeader{
		UserName: userName,
		DateTime: time.Now().Format(time.RFC3339),
	}
	if err := writeDelimitedTo(header, stream); err != nil {
		stream.Close()
		return nil, err
	}

	// Store start
	if err := writeDelimitedTo(request, stream); err != nil {
		stream.Close()
		return nil, err
	}
	if err := writeDelimitedTo(response, stream); err != nil {
		stream.Close()
		return nil, err
	}

	return &ReplayFile{stream: stream}, nil
}

func (r *ReplayFile) Close() error {
	return r.stream.Close()
}

func (r *ReplayFile) Append(request *swoq.ActRequest, response *swoq.ActResponse) error {
	if err := writeDelimitedTo(request, r.stream); err != nil {
		return err
	}
	if err := writeDelimitedTo(response, r.stream); err != nil {
		return err
	}
	return nil
}

// Helper for writing delimited protobuf messages
func writeDelimitedTo(msg proto.Message, writer io.Writer) error {
	// Serialize the message
	data, err := proto.Marshal(msg)
	if err != nil {
		return err
	}

	// Write the varint length
	varintBuf := make([]byte, binary.MaxVarintLen64)
	varintLen := binary.PutUvarint(varintBuf, uint64(len(data)))
	if _, err := writer.Write(varintBuf[:varintLen]); err != nil {
		return err
	}

	// Write the serialized message
	_, err = writer.Write(data)
	return err
}
